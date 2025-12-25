using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Orbitrap.Abstractions;
using Orbitrap.Abstractions.Diagnostics;
using Orbitrap.Mock.Configuration;

namespace Orbitrap.Mock;

/// <summary>
/// Mock implementation of IOrbitrapInstrument for development and testing.
/// Uses Channel-based buffering for backpressure handling.
/// </summary>
public sealed class MockOrbitrapInstrument : IOrbitrapInstrument
{
    private readonly MockOptions _options;
    private readonly Channel<IOrbitrapScan> _scanChannel;
    private readonly object _stateLock = new();

    private AcquisitionState _currentState = AcquisitionState.Idle;
    private MockAcquisitionSession? _currentSession;
    private bool _disposed;

    public MockOrbitrapInstrument(IOptions<MockOptions> options)
        : this(options.Value)
    {
    }

    public MockOrbitrapInstrument(MockOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Create bounded channel for backpressure
        _scanChannel = Channel.CreateBounded<IOrbitrapScan>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

    public string InstrumentName => _options.InstrumentName;
    public string InstrumentId => _options.InstrumentId;

    public AcquisitionState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                _currentState = value;
            }
        }
    }

    public event EventHandler<OrbitrapScanEventArgs>? ScanArrived;
    public event EventHandler<OrbitrapScanEventArgs>? Ms1ScanArrived;
    public event EventHandler<OrbitrapScanEventArgs>? Ms2ScanArrived;

    public async Task<IAcquisitionSession> StartAcquisitionAsync(
        AcquisitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_stateLock)
        {
            if (_currentState != AcquisitionState.Idle && _currentState != AcquisitionState.Completed)
            {
                throw new InvalidOperationException(
                    $"Cannot start acquisition in state {_currentState}. Must be Idle or Completed.");
            }

            _currentState = AcquisitionState.Starting;
        }

        options ??= new AcquisitionOptions();

        var session = new MockAcquisitionSession(
            this,
            options,
            cancellationToken);

        _currentSession = session;
        CurrentState = AcquisitionState.Acquiring;

        // Start the background scan generation task
        _ = Task.Run(() => session.RunAsync(), cancellationToken);

        await Task.CompletedTask;
        return session;
    }

    public async IAsyncEnumerable<IOrbitrapScan> GetScansAsync(
        ScanFilter? filter = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await foreach (var scan in _scanChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (filter is null || filter.Matches(scan))
            {
                yield return scan;
            }
        }
    }

    /// <summary>
    /// Emits a scan to all subscribers and the channel.
    /// Called internally by MockAcquisitionSession.
    /// </summary>
    internal async ValueTask EmitScanAsync(IOrbitrapScan scan, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        // Record metrics
        OrbitrapMetrics.RecordScanReceived(scan);

        // Write to channel
        await _scanChannel.Writer.WriteAsync(scan, cancellationToken);

        // Fire events
        var args = new OrbitrapScanEventArgs(scan);

        ScanArrived?.Invoke(this, args);

        if (scan.MsOrder == 1)
        {
            Ms1ScanArrived?.Invoke(this, args);
        }
        else
        {
            Ms2ScanArrived?.Invoke(this, args);
        }
    }

    internal void SetState(AcquisitionState state)
    {
        CurrentState = state;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _scanChannel.Writer.TryComplete();
        _currentSession?.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _scanChannel.Writer.TryComplete();

        if (_currentSession is not null)
        {
            await _currentSession.DisposeAsync();
        }

        _disposed = true;
    }
}

/// <summary>
/// Mock acquisition session with simulated scan generation.
/// </summary>
internal sealed class MockAcquisitionSession : IAcquisitionSession, IDisposable
{
    private readonly MockOrbitrapInstrument _instrument;
    private readonly AcquisitionOptions _options;
    private readonly CancellationTokenSource _cts;
    private readonly TaskCompletionSource _completionSource = new();
    private readonly Channel<IOrbitrapScan> _sessionChannel;

    private long _scanCount;
    private Exception? _error;
    private bool _disposed;

    public MockAcquisitionSession(
        MockOrbitrapInstrument instrument,
        AcquisitionOptions options,
        CancellationToken externalToken)
    {
        _instrument = instrument;
        _options = options;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

        _sessionChannel = Channel.CreateBounded<IOrbitrapScan>(
            new BoundedChannelOptions(options.BufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

        SessionId = Guid.NewGuid().ToString("N")[..8];
    }

    public string SessionId { get; }

    public AcquisitionState State => _instrument.CurrentState;

    public long ScanCount => Interlocked.Read(ref _scanCount);

    public Task Completion => _completionSource.Task;

    public Exception? Error => _error;

    public IAsyncEnumerable<IOrbitrapScan> Scans => _sessionChannel.Reader.ReadAllAsync(_cts.Token);

    public async Task RunAsync()
    {
        using var activity = OrbitrapTracing.StartAcquisition(SessionId, _instrument.InstrumentId);

        try
        {
            var scanNumber = 1;
            var retentionTime = 0.0;
            var random = new Random();
            var startTime = DateTime.UtcNow;

            while (!_cts.Token.IsCancellationRequested)
            {
                // Check max scans
                if (_options.MaxScans.HasValue && _scanCount >= _options.MaxScans.Value)
                {
                    break;
                }

                // Check max duration
                if (_options.MaxDuration.HasValue &&
                    DateTime.UtcNow - startTime >= _options.MaxDuration.Value)
                {
                    break;
                }

                // Generate MS1 scan
                var ms1Scan = GenerateMs1Scan(scanNumber++, retentionTime, random);
                await EmitScanAsync(ms1Scan);

                // Generate 3-5 MS2 scans per MS1
                var ms2Count = random.Next(3, 6);
                for (int i = 0; i < ms2Count && !_cts.Token.IsCancellationRequested; i++)
                {
                    if (_options.MaxScans.HasValue && _scanCount >= _options.MaxScans.Value)
                        break;

                    var ms2Scan = GenerateMs2Scan(scanNumber++, retentionTime, random, ms1Scan);
                    await EmitScanAsync(ms2Scan);
                }

                retentionTime += 0.5 / 60.0; // ~0.5 seconds per cycle = ~0.5/60 minutes

                // Simulate realistic timing (~2 Hz for MS1)
                await Task.Delay(500, _cts.Token);
            }

            _instrument.SetState(AcquisitionState.Completed);
            _sessionChannel.Writer.Complete();
            _completionSource.SetResult();
        }
        catch (OperationCanceledException)
        {
            _instrument.SetState(AcquisitionState.Completed);
            _sessionChannel.Writer.Complete();
            _completionSource.SetResult();
        }
        catch (Exception ex)
        {
            _error = ex;
            _instrument.SetState(AcquisitionState.Faulted);
            _sessionChannel.Writer.Complete(ex);
            _completionSource.SetException(ex);
        }
    }

    private async Task EmitScanAsync(IOrbitrapScan scan)
    {
        var scanToEmit = _options.AutoFreeze ? scan.ToFrozen() : scan;

        await _instrument.EmitScanAsync(scanToEmit, _cts.Token);
        await _sessionChannel.Writer.WriteAsync(scanToEmit, _cts.Token);

        Interlocked.Increment(ref _scanCount);
    }

    private static MockMsScan GenerateMs1Scan(int scanNumber, double retentionTime, Random random)
    {
        // Generate realistic-looking MS1 spectrum
        var peakCount = random.Next(500, 2000);
        var mzValues = new double[peakCount];
        var intensityValues = new double[peakCount];

        var baseMz = 400 + random.NextDouble() * 800; // 400-1200 m/z range
        var baseIntensity = 1e6 + random.NextDouble() * 1e7;

        for (int i = 0; i < peakCount; i++)
        {
            mzValues[i] = 200 + random.NextDouble() * 1800; // 200-2000 m/z
            intensityValues[i] = random.NextDouble() * baseIntensity * 0.1;
        }

        // Sort by m/z
        Array.Sort(mzValues, intensityValues);

        // Find base peak
        var maxIdx = 0;
        var maxInt = intensityValues[0];
        var tic = 0.0;
        for (int i = 0; i < peakCount; i++)
        {
            tic += intensityValues[i];
            if (intensityValues[i] > maxInt)
            {
                maxInt = intensityValues[i];
                maxIdx = i;
            }
        }

        return new MockMsScanBuilder()
            .WithScanNumber(scanNumber)
            .WithMsOrder(1)
            .WithRetentionTime(retentionTime)
            .WithSpectrum(mzValues, intensityValues)
            .WithBasePeak(mzValues[maxIdx], maxInt)
            .WithTotalIonCurrent(tic)
            .WithAnalyzer("Orbitrap", 120000, 3.0)
            .WithPolarity(Polarity.Positive)
            .Build();
    }

    private static MockMsScan GenerateMs2Scan(
        int scanNumber,
        double retentionTime,
        Random random,
        IOrbitrapScan precursorScan)
    {
        // Pick a random precursor from MS1
        var precursorIdx = random.Next(0, precursorScan.PeakCount);
        var precursorMz = precursorScan.MzValues.Span[precursorIdx];
        var precursorIntensity = precursorScan.IntensityValues.Span[precursorIdx];
        var charge = random.Next(2, 5);

        // Generate fragment spectrum
        var peakCount = random.Next(50, 300);
        var mzValues = new double[peakCount];
        var intensityValues = new double[peakCount];

        var baseIntensity = precursorIntensity * 0.5;

        for (int i = 0; i < peakCount; i++)
        {
            // Fragments are typically lower m/z than precursor
            mzValues[i] = 100 + random.NextDouble() * (precursorMz - 100);
            intensityValues[i] = random.NextDouble() * baseIntensity;
        }

        Array.Sort(mzValues, intensityValues);

        var maxIdx = 0;
        var maxInt = intensityValues[0];
        var tic = 0.0;
        for (int i = 0; i < peakCount; i++)
        {
            tic += intensityValues[i];
            if (intensityValues[i] > maxInt)
            {
                maxInt = intensityValues[i];
                maxIdx = i;
            }
        }

        return new MockMsScanBuilder()
            .WithScanNumber(scanNumber)
            .WithRetentionTime(retentionTime)
            .WithSpectrum(mzValues, intensityValues)
            .WithBasePeak(mzValues[maxIdx], maxInt)
            .WithTotalIonCurrent(tic)
            .WithPrecursor(precursorMz, charge, precursorIntensity, 1.6, 30.0)
            .WithAnalyzer("Orbitrap", 30000, 5.0) // Lower resolution for MS2
            .WithPolarity(Polarity.Positive)
            .Build();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _instrument.SetState(AcquisitionState.Stopping);
        _cts.Cancel();
        return Completion;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        // Simple mock doesn't support pause
        throw new NotSupportedException("Mock instrument does not support pause");
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Mock instrument does not support resume");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cts.Cancel();
        _cts.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _cts.CancelAsync();
        _cts.Dispose();
        _disposed = true;
    }
}
