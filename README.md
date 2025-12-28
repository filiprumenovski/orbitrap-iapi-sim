# Orbitrap IAPI Simulator


---

## Overview

This project provides a **thin-waist abstraction layer** that unifies real ThermoFisher Orbitrap instruments and simulated data sources behind a single interface. Downstream analysis pipelines—MSGF+ integration, mzML export, real-time dashboards—consume `IOrbitrapScan` without knowledge of the underlying data source.

```
REAL HARDWARE                          SIMULATION
─────────────                          ──────────
Orbitrap Exploris 480                  Rust gRPC Server
        ↓                                    ↓
Thermo IAPI DLL                        MockOrbitrapInstrument
        ↓                                    ↓
RealOrbitrapAdapter                    MockScanAdapter
        ↓                                    ↓
            ╔═══════════════════════════════╗
            ║      IOrbitrapScan            ║  ← Unified Interface
            ║      IOrbitrapInstrument      ║
            ╚═══════════════════════════════╝
                          ↓
              Your Analysis Pipeline
```

**Key capabilities:**

- Zero-copy spectrum data via `ReadOnlyMemory<double>`
- Async streams (`IAsyncEnumerable`) with backpressure handling
- Channel-based buffering for high-throughput scenarios
- OpenTelemetry metrics and distributed tracing
- Thread-safe immutable scan snapshots
- Cross-platform mock (macOS/Linux/Windows), Windows-only real adapter

---

## Table of Contents

- [Quick Start](#quick-start)
- [Installation](#installation)
- [Architecture](#architecture)
- [API Reference](#api-reference)
- [Configuration](#configuration)
- [Rust Simulator](#rust-simulator)
- [Docker](#docker)
- [Testing](#testing)
- [CI/CD](#cicd)
- [Performance](#performance)
- [Contributing](#contributing)
- [License](#license)

---

## Quick Start

### Prerequisites

| Tool | Version | Required |
|------|---------|----------|
| .NET SDK | 10.0+ | Yes |
| Rust | 1.75+ | Optional (for simulator) |
| Docker | 24.0+ | Optional |
| just | 1.0+ | Optional (or use `make`) |

### Build and Run

```bash
# Clone
git clone https://github.com/yourusername/orbitrap-iapi-sim.git
cd orbitrap-iapi-sim

# Build
dotnet build

# Run tests
dotnet test

# Run sample application
dotnet run --project samples/Orbitrap.Console
```

### VirtualOrbitrap Pipeline (mzML → VirtualRawData)

This repo also includes a Virtual Orbitrap pipeline that reads mzML and produces Orbitrap-like scan objects
(`VirtualRawData`, `ScanInfo`, `CentroidStream`) with optional enrichment (resolution/noise/baseline/filter strings).

```bash
# Run the pipeline against the included tiny mzML fixture
dotnet run -c Release --project samples/VirtualOrbitrap.BasicUsage/VirtualOrbitrap.BasicUsage.csproj -- data/sample.mzML

# Headless streaming replay with ScanArrived events
dotnet run -c Release --project samples/VirtualOrbitrap.StreamingSimulation/VirtualOrbitrap.StreamingSimulation.csproj -- data/sample.mzML immediate 1.0
```

Note: the built-in mzML loader is intentionally minimal and currently supports uncompressed base64 arrays.
If your mzML uses zlib-compressed binary arrays, the loader will need an extension.

### Minimal Example

```csharp
using Orbitrap.Abstractions;
using Orbitrap.Integration;

// Create mock instrument
await using var instrument = InstrumentFactory.CreateMockDefault();

// Start acquisition
await using var session = await instrument.StartAcquisitionAsync(
    new AcquisitionOptions { MaxScans = 100 });

// Process scans via async stream
await foreach (var scan in session.Scans)
{
    Console.WriteLine($"Scan #{scan.ScanNumber} MS{scan.MsOrder} " +
                      $"RT={scan.RetentionTime:F2}min " +
                      $"Peaks={scan.PeakCount}");
}
```

---

## Installation

### NuGet Packages

```bash
# Core abstractions (interfaces only)
dotnet add package Orbitrap.Abstractions

# Mock implementation for development
dotnet add package Orbitrap.Mock

# Full integration with factory and DI
dotnet add package Orbitrap.Integration
```

### Package Dependencies

```
Orbitrap.Abstractions
    └── System.Diagnostics.DiagnosticSource (OpenTelemetry support)

Orbitrap.Mock
    ├── Orbitrap.Abstractions
    ├── Grpc.Net.Client
    ├── Google.Protobuf
    └── Microsoft.Extensions.Options

Orbitrap.Integration
    ├── Orbitrap.Abstractions
    ├── Orbitrap.Mock
    ├── Microsoft.Extensions.DependencyInjection.Abstractions
    └── OpenTelemetry
```

---

## Architecture

### The Thin Waist Pattern

All downstream code depends exclusively on two interfaces:

```csharp
public interface IOrbitrapScan
{
    // Identification
    int ScanNumber { get; }
    int MsOrder { get; }                    // 1 = MS1, 2+ = MSn
    double RetentionTime { get; }           // minutes

    // Spectrum data (zero-copy capable)
    ReadOnlyMemory<double> MzValues { get; }
    ReadOnlyMemory<double> IntensityValues { get; }
    int PeakCount { get; }

    // Aggregates
    double BasePeakMz { get; }
    double BasePeakIntensity { get; }
    double TotalIonCurrent { get; }

    // Precursor (MS2+ only)
    double? PrecursorMass { get; }
    int? PrecursorCharge { get; }
    double? IsolationWidth { get; }
    double? CollisionEnergy { get; }
    FragmentationType? FragmentationType { get; }

    // Analyzer metadata
    string Analyzer { get; }
    double ResolutionAtMz200 { get; }
    double MassAccuracyPpm { get; }
    Polarity Polarity { get; }

    // Extended metadata
    IReadOnlyDictionary<string, string> TrailerExtra { get; }

    // Immutability
    FrozenOrbitrapScan ToFrozen();
}

public interface IOrbitrapInstrument : IAsyncDisposable, IDisposable
{
    string InstrumentName { get; }
    string InstrumentId { get; }
    AcquisitionState CurrentState { get; }

    // Event-based API
    event EventHandler<OrbitrapScanEventArgs>? ScanArrived;
    event EventHandler<OrbitrapScanEventArgs>? Ms1ScanArrived;
    event EventHandler<OrbitrapScanEventArgs>? Ms2ScanArrived;

    // Modern async API
    Task<IAcquisitionSession> StartAcquisitionAsync(
        AcquisitionOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IOrbitrapScan> GetScansAsync(
        ScanFilter? filter = null,
        CancellationToken cancellationToken = default);
}
```

### Project Structure

```
orbitrap-iapi-sim/
├── Directory.Build.props
├── .dockerignore
├── global.json
├── Orbitrap.sln
├── README.md
├── Makefile
├── justfile
├── run-tests.sh
├── data/
│   └── sample.mzML
├── docker/
│   ├── Dockerfile.console
│   ├── Dockerfile.simulator
│   ├── docker-compose.yml
│   ├── prometheus.yml
│   └── .dockerignore
├── protos/
│   └── simulator.proto
├── samples/
│   ├── Orbitrap.Console/
│   ├── VirtualOrbitrap.BasicUsage/
│   └── VirtualOrbitrap.StreamingSimulation/
├── src/
│   ├── dotnet/
│   │   ├── Orbitrap.Abstractions/
│   │   ├── Orbitrap.Integration/
│   │   ├── Orbitrap.Mock/
│   │   ├── Orbitrap.Real/
│   │   ├── VirtualOrbitrap.Builders/
│   │   ├── VirtualOrbitrap.Enrichment/
│   │   ├── VirtualOrbitrap.IAPI/
│   │   ├── VirtualOrbitrap.Parsers/
│   │   ├── VirtualOrbitrap.Pipeline/
│   │   └── VirtualOrbitrap.Schema/
│   └── rust/
│       └── lc-ms-simulator/
└── tests/
    ├── Orbitrap.Abstractions.Tests/
    ├── Orbitrap.Integration.Tests/
    ├── Orbitrap.Mock.Tests/
    └── VirtualOrbitrap.Tests/
```

### Data Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ACQUISITION SESSION                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Instrument          Channel<IOrbitrapScan>          Consumer       │
│  ──────────          ─────────────────────          ────────        │
│                                                                     │
│  [Generate] ──▶ [Bounded Buffer: 1000] ──▶ [await foreach]          │
│     │                     │                       │                 │
│     │                     │ Backpressure          │                 │
│     │◀────────────────────┘                       │                 │
│     │                                             │                 │
│     ├──▶ ScanArrived event ───────────────────────┤                 │
│     ├──▶ Ms1ScanArrived event ────────────────────┤                 │
│     └──▶ Ms2ScanArrived event ────────────────────┘                 │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## API Reference

### Acquisition Lifecycle

```csharp
// 1. Create instrument via factory
using var instrument = InstrumentFactory.Create(new InstrumentOptions
{
    Mode = InstrumentMode.Mock,
    Mock = new MockOptions
    {
        InstrumentName = "Exploris 480",
        Host = "localhost",
        Port = 31417
    }
});

// 2. Configure acquisition
var options = new AcquisitionOptions
{
    MaxScans = 10000,                    // Stop after N scans
    MaxDuration = TimeSpan.FromHours(1), // Or after duration
    BufferCapacity = 2000,               // Channel buffer size
    AutoFreeze = true,                   // Thread-safe copies
    Filter = new ScanFilter { MsOrder = 1 } // MS1 only
};

// 3. Start acquisition session
await using var session = await instrument.StartAcquisitionAsync(options);

// 4. Consume scans
await foreach (var scan in session.Scans)
{
    ProcessScan(scan);
}

// 5. Session auto-disposes, or call:
await session.StopAsync();
```

### Event-Based API

```csharp
instrument.Ms1ScanArrived += (sender, e) =>
{
    var scan = e.Scan;
    Console.WriteLine($"MS1: {scan.TotalIonCurrent:E2}");
};

instrument.Ms2ScanArrived += (sender, e) =>
{
    var scan = e.Scan;
    Console.WriteLine($"MS2: Precursor {scan.PrecursorMass:F4} m/z");
};

await instrument.StartAcquisitionAsync();
```

### Async Stream with LINQ

```csharp
// Filter and transform with System.Linq.Async
var highIntensityMs2 = await instrument
    .GetScansAsync(cancellationToken: cts.Token)
    .Where(s => s.MsOrder == 2)
    .Where(s => s.TotalIonCurrent > 1e7)
    .Select(s => s.ToFrozen())
    .Take(100)
    .ToListAsync();
```

### Immutable Snapshots

```csharp
// Scans from mock may be reused/mutated
// ToFrozen() creates a thread-safe copy
var frozen = scan.ToFrozen();

// Safe to pass across threads, store in collections
Task.Run(() => ProcessAsync(frozen));

// FrozenOrbitrapScan uses:
// - Defensive array copies
// - FrozenDictionary<string, string> for TrailerExtra
```

### Scan Filtering

```csharp
var filter = new ScanFilter
{
    MsOrder = 2,                        // MS2 only
    MinRetentionTime = 10.0,            // After 10 min
    MaxRetentionTime = 60.0,            // Before 60 min
    Polarity = Polarity.Positive,       // Positive mode
    Analyzer = "Orbitrap"               // Specific analyzer
};

// Apply to acquisition
var session = await instrument.StartAcquisitionAsync(
    new AcquisitionOptions { Filter = filter });

// Or check manually
if (filter.Matches(scan))
{
    ProcessScan(scan);
}
```

### Result Type for Error Handling

```csharp
// Explicit success/failure without exceptions
ScanResult result = await GetScanAsync();

// Pattern matching
var message = result.Match(
    onSuccess: scan => $"Got scan #{scan.ScanNumber}",
    onFailure: error => $"Error: {error.Code} - {error.Message}");

// Or throw on failure
IOrbitrapScan scan = result.GetScanOrThrow();

// Predefined error codes
ScanError.ReadError       // "SCAN_READ_ERROR"
ScanError.Timeout         // "SCAN_TIMEOUT"
ScanError.BufferOverflow  // "SCAN_BUFFER_OVERFLOW"
ScanError.Disconnected    // "SCAN_DISCONNECTED"
```

### Builder Pattern for Test Data

```csharp
var ms2Scan = new MockMsScanBuilder()
    .WithScanNumber(42)
    .WithRetentionTime(25.3)
    .WithSpectrum(mzValues, intensityValues)  // Auto-calculates aggregates
    .WithPrecursor(
        mass: 856.4231,
        charge: 2,
        intensity: 1.5e6,
        isolationWidth: 1.6,
        collisionEnergy: 30.0,
        fragmentationType: FragmentationType.HCD)
    .WithAnalyzer("Orbitrap", resolution: 60000, massAccuracy: 5.0)
    .WithPolarity(Polarity.Positive)
    .WithTrailerExtra("Scan Description", "Full ms2 856.42@hcd30.00")
    .UseArrayPool(true)  // Reduce GC pressure
    .Build();
```

---

## Configuration

### Dependency Injection

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Option 1: Configure via action
builder.Services.AddOrbitrapInstrument(options =>
{
    options.Mode = InstrumentMode.Mock;
    options.Mock = new MockOptions
    {
        Host = "localhost",
        Port = 31417,
        InstrumentName = "Development Orbitrap"
    };
});

// Option 2: Bind from configuration
builder.Services.Configure<InstrumentOptions>(
    builder.Configuration.GetSection("Instrument"));
builder.Services.AddSingleton<IOrbitrapInstrument>(sp =>
    InstrumentFactory.Create(sp.GetRequiredService<IOptions<InstrumentOptions>>()));

// Option 3: Quick mock for testing
builder.Services.AddMockOrbitrapInstrumentDefault();
```

### appsettings.json

```json
{
  "Instrument": {
    "Mode": "Mock",
    "Mock": {
      "Host": "localhost",
      "Port": 31417,
      "UseTls": false,
      "ConnectionTimeout": "00:00:30",
      "InstrumentName": "Simulated Exploris 480",
      "InstrumentId": "SIM-001",
      "Retry": {
        "MaxRetries": 3,
        "InitialDelay": "00:00:01",
        "MaxDelay": "00:00:30",
        "BackoffMultiplier": 2.0
      }
    },
    "Real": {
      "ComPort": "COM1",
      "BaudRate": 9600,
      "NetworkAddress": null,
      "ConnectionTimeout": "00:01:00",
      "IapiPath": null
    }
  }
}
```

### Environment Variables

```bash
# Override configuration
export Instrument__Mode=Mock
export Instrument__Mock__Host=simulator.local
export Instrument__Mock__Port=31417
```

---

## Rust Simulator

The Rust gRPC server generates realistic LC-MS/MS data with configurable parameters.

### Build and Run

```bash
cd src/rust/lc-ms-simulator

# Build
cargo build --release

# Run with defaults
./target/release/lc-ms-simulator

# Custom configuration
./target/release/lc-ms-simulator \
    --host 0.0.0.0 \
    --port 31417 \
    --instrument-name "Simulated Exploris 480" \
    --instrument-id "SIM-001" \
    --log-level debug
```

### gRPC API

```protobuf
service SimulatorService {
    // Stream scans from the simulator
    rpc StreamScans(StreamScansRequest) returns (stream ScanMessage);

    // Control acquisition
    rpc StartAcquisition(StartAcquisitionRequest) returns (StartAcquisitionResponse);
    rpc StopAcquisition(StopAcquisitionRequest) returns (StopAcquisitionResponse);

    // Status and info
    rpc GetStatus(GetStatusRequest) returns (StatusResponse);
    rpc GetInstrumentInfo(GetInstrumentInfoRequest) returns (InstrumentInfoResponse);
}
```

### Simulation Parameters

```protobuf
message SimulationParameters {
    double scan_rate = 1;        // Scans per second (default: 2.0)
    int32 ms2_per_ms1 = 2;       // MS2 scans per MS1 (default: 4)
    double min_mz = 3;           // m/z range start (default: 200)
    double max_mz = 4;           // m/z range end (default: 2000)
    double resolution = 5;       // Resolution at m/z 200 (default: 120000)
    double noise_level = 6;      // Noise as fraction of signal (default: 0.01)
    int64 random_seed = 7;       // For reproducibility (0 = random)
}
```

---

## Docker

### Quick Start

```bash
# Start simulator only
docker-compose -f docker/docker-compose.yml up simulator

# Start with observability stack (Jaeger, Prometheus, Grafana)
docker-compose -f docker/docker-compose.yml --profile observability up

# Build images locally
docker build -t orbitrap-simulator -f docker/Dockerfile.simulator .
docker build -t orbitrap-console -f docker/Dockerfile.console .
```

### Container Images

| Image | Size | Description |
|-------|------|-------------|
| `ghcr.io/yourusername/orbitrap-iapi-sim/simulator` | ~15MB | Rust gRPC server |
| `ghcr.io/yourusername/orbitrap-iapi-sim/console` | ~85MB | .NET console demo |

### Docker Compose Services

```yaml
services:
  simulator:     # Rust gRPC server on :31417
  console:       # .NET console application
  jaeger:        # Distributed tracing UI on :16686
  prometheus:    # Metrics collection on :9090
  grafana:       # Dashboards on :3000
```

---

## Testing

### Run All Tests

```bash
# Using just (recommended)
just test

# Using make
make test

# Using dotnet directly
dotnet test Orbitrap.sln --configuration Release
```

### Test Coverage

```bash
just test-coverage
# Reports saved to ./coverage/
```

### Test Projects

| Project | Tests | Coverage |
|---------|-------|----------|
| `Orbitrap.Abstractions.Tests` | 32 | FrozenOrbitrapScan, ScanFilter, ScanResult, EventArgs |
| `Orbitrap.Mock.Tests` | 47 | MockMsScan, MockMsScanBuilder, MockOrbitrapInstrument, Options |
| `Orbitrap.Integration.Tests` | 20 | InstrumentFactory, ServiceCollectionExtensions |

### Test Categories

```bash
# Run specific test class
just test-filter "MockOrbitrapInstrumentTests"

# Run by trait
dotnet test --filter "Category=Integration"
```

### Example Test

```csharp
[Fact]
public async Task StartAcquisitionAsync_WithMaxScans_StopsAfterMaxScans()
{
    // Arrange
    using var instrument = CreateInstrument();
    var maxScans = 10;

    // Act
    await using var session = await instrument.StartAcquisitionAsync(
        new AcquisitionOptions { MaxScans = maxScans });
    await session.Completion;

    // Assert
    session.ScanCount.Should().BeLessOrEqualTo(maxScans);
    instrument.CurrentState.Should().Be(AcquisitionState.Completed);
}
```

---

## CI/CD

### GitHub Actions Workflows

| Workflow | Trigger | Jobs |
|----------|---------|------|
| `ci.yml` | Push, PR | Build, test, lint, coverage, security audit |
| `release.yml` | Tag `v*` | Multi-platform binaries, NuGet, Docker, GitHub Release |
| `pr-check.yml` | PR | Fast validation, change detection |

### Pipeline Stages

```
┌─────────────────────────────────────────────────────────────────────┐
│                           CI PIPELINE                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐             │
│  │ Restore │──▶│  Build  │──▶│  Test   │──▶│  Lint   │             │
│  └─────────┘   └─────────┘   └─────────┘   └─────────┘             │
│       │                           │             │                   │
│       │    ┌──────────────────────┼─────────────┘                   │
│       ▼    ▼                      ▼                                 │
│  ┌─────────────┐          ┌─────────────┐                           │
│  │  Coverage   │          │  Security   │                           │
│  │  (Codecov)  │          │   Audit     │                           │
│  └─────────────┘          └─────────────┘                           │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        RELEASE PIPELINE                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Tag v*                                                             │
│    │                                                                │
│    ▼                                                                │
│  ┌─────────┐                                                        │
│  │Validate │                                                        │
│  └────┬────┘                                                        │
│       │                                                             │
│       ├────────────┬────────────┬────────────┐                      │
│       ▼            ▼            ▼            ▼                      │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐                │
│  │  NuGet  │  │ .NET    │  │  Rust   │  │ Docker  │                │
│  │ Package │  │Binaries │  │Binaries │  │ Images  │                │
│  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘                │
│       │            │            │            │                      │
│       └────────────┴────────────┴────────────┘                      │
│                         │                                           │
│                         ▼                                           │
│                  ┌─────────────┐                                    │
│                  │   GitHub    │                                    │
│                  │   Release   │                                    │
│                  └──────┬──────┘                                    │
│                         │                                           │
│                         ▼                                           │
│                  ┌─────────────┐                                    │
│                  │   NuGet     │                                    │
│                  │   Publish   │                                    │
│                  └─────────────┘                                    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Build Matrix

| Platform | .NET | Rust |
|----------|------|------|
| ubuntu-latest | ✅ | ✅ |
| windows-latest | ✅ | ✅ |
| macos-latest | ✅ | ✅ |

### Release Artifacts

| Artifact | Platforms |
|----------|-----------|
| NuGet packages | Any (netstandard2.0, net8.0) |
| .NET binaries | linux-x64, linux-arm64, win-x64, osx-x64, osx-arm64 |
| Rust binaries | x86_64-linux, x86_64-windows, x86_64-darwin, aarch64-darwin |
| Docker images | linux/amd64, linux/arm64 |

---

## Performance

### Memory Efficiency

| Feature | Benefit |
|---------|---------|
| `ReadOnlyMemory<double>` | Zero-copy slicing, no defensive copies |
| `ArrayPool<double>.Shared` | Reduced GC pressure for high-frequency scans |
| `FrozenDictionary` | O(1) lookup, optimized for read-heavy access |
| `Channel<T>` bounded buffer | Backpressure prevents OOM |

### Benchmarks

```
| Method                  | Mean      | Allocated |
|------------------------ |----------:|----------:|
| CreateMockScan          |   1.2 μs  |     2 KB  |
| CreateMockScan_Pooled   |   0.8 μs  |     0 KB  |
| ToFrozen                |   2.1 μs  |     4 KB  |
| ScanFilter.Matches      |   12 ns   |     0 KB  |
| Channel.WriteAsync      |   45 ns   |     0 KB  |
```

### Throughput

The mock instrument sustains **2,000+ scans/second** with default settings. The Rust simulator achieves **10,000+ scans/second** in streaming mode.

---

## Observability

### OpenTelemetry Metrics

```csharp
// Built-in metrics (Orbitrap.Simulator meter)
orbitrap.scans.received      // Counter: total scans by ms_order, analyzer
orbitrap.scans.processed     // Counter: processed by consumer
orbitrap.scans.dropped       // Counter: dropped due to backpressure
orbitrap.scan.processing_time // Histogram: processing latency (ms)
orbitrap.scan.peak_count     // Histogram: peaks per scan
orbitrap.scan.tic            // Histogram: total ion current
orbitrap.buffer.depth        // Gauge: current buffer utilization
```

### Distributed Tracing

```csharp
// Built-in activities (Orbitrap.Simulator source)
Acquisition          // Span: entire acquisition session
ProcessScan          // Span: individual scan processing

// Span attributes
scan.number, scan.ms_order, scan.retention_time,
scan.peak_count, scan.analyzer, scan.polarity,
scan.precursor_mz, scan.precursor_charge
```

### Configuration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(OrbitrapTracing.SourceName)
        .AddJaegerExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(OrbitrapMetrics.MeterName)
        .AddPrometheusExporter());
```

---

## Contributing

### Development Setup

```bash
# Install tools
brew install just         # or: cargo install just
brew install dotnet-sdk
brew install rust

# Setup
just setup

# Development workflow
just build
just test
just lint
```

### Code Style

- **C#**: Enforced via `.editorconfig` and `dotnet format`
- **Rust**: Enforced via `rustfmt` and `clippy`
- **Commits**: [Conventional Commits](https://www.conventionalcommits.org/)

### Pull Request Checklist

- [ ] Tests pass (`just test`)
- [ ] Linting passes (`just lint`)
- [ ] Documentation updated
- [ ] CHANGELOG.md updated (if applicable)

---

## Roadmap

- [ ] Real Thermo IAPI adapter implementation
- [ ] mzML export integration
- [ ] MSGF+ pipeline integration
- [ ] Web-based dashboard
- [ ] gRPC streaming client for Rust simulator
- [ ] Native AOT compilation support

---

## License

MIT License. See [LICENSE](LICENSE) for details.

---

## Acknowledgments

- ThermoFisher Scientific for the Orbitrap IAPI specification
- The proteomics community for domain expertise
- OpenTelemetry and .NET teams for observability primitives

---

<p align="center">
  <sub>Built for mass spectrometry. Engineered for reliability.</sub>
</p>
