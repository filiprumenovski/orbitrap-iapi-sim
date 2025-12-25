# 🚀 SHIP THE UNIVERSE: Orbitrap IAPI Simulator - Senior Dev Spec
## WITH C# SHIM & RICH .NET OBJECT FACTORY + UNIFIED ABSTRACTION

**You are a Staff Engineer at a $10B biotech unicorn. Ship a production-grade ThermoFisher Orbitrap IAPI mock simulator TODAY. No prototypes. No half-measures. Battle-tested code only.**

---

## 🎯 THE THIN WAIST: Unified IOrbitrapScan

**This is where mock and real converge.** Your entire downstream system (MSGF+ integration, analysis pipelines, dashboards) consumes ONLY this interface:

```csharp
// CRITICAL: This interface is the contract.
// Both REAL Thermo IAPI and your MOCK IAPI produce objects matching this.
// Downstream code NEVER sees MockMsScan or real MsScan directly.

namespace ThermoFisher.CommonCore.Data.Business.Abstraction
{
    /// <summary>
    /// Unified scan interface: real Orbitrap and mock both implement this.
    /// Downstream code depends ONLY on this, not on specific implementations.
    /// </summary>
    public interface IOrbitrapScan
    {
        // Identification
        int ScanNumber { get; }
        int MsOrder { get; }              // 1 = MS1, 2 = MS2
        double RetentionTime { get; }     // minutes
        
        // Spectrum data
        double[] MzValues { get; }
        double[] IntensityValues { get; }
        int PeakCount { get; }
        
        // Aggregates
        double BasePeakMz { get; }
        double BasePeakIntensity { get; }
        double TotalIonCurrent { get; }
        
        // Precursor (MS2 only)
        double? PrecursorMass { get; }
        int? PrecursorCharge { get; }
        double? PrecursorIntensity { get; }
        double? IsolationWidth { get; }
        
        // Analyzer metadata
        string Analyzer { get; }
        double ResolutionAtMz200 { get; }
        double MassAccuracyPpm { get; }
        
        // Extended metadata
        IReadOnlyDictionary<string, string> TrailerExtra { get; }
    }

    /// <summary>
    /// Event args carrying IOrbitrapScan, used by both real and mock.
    /// </summary>
    public class OrbitrapScanEventArgs : EventArgs
    {
        public OrbitrapScanEventArgs(IOrbitrapScan scan) => Scan = scan;
        public IOrbitrapScan Scan { get; }
    }

    /// <summary>
    /// Unified instrument interface: real Orbitrap and mock both implement this.
    /// </summary>
    public interface IOrbitrapInstrument : IDisposable
    {
        string InstrumentName { get; }
        string InstrumentId { get; }
        
        event EventHandler<OrbitrapScanEventArgs> Ms1ScanArrived;
        event EventHandler<OrbitrapScanEventArgs> Ms2ScanArrived;
        
        void StartAcquisition();
        void StopAcquisition();
        AcquisitionState CurrentState { get; }
    }

    public enum AcquisitionState { Idle, Acquiring, Paused, Stopping }
}
```

---

## 🔄 THE CONVERGENCE PATTERN

Two separate implementations, ONE shared interface:

```csharp
// IMPLEMENTATION A: Real Thermo IAPI
namespace ThermoFisher.CommonCore.Data.Business.Real
{
    public class RealOrbitrapAdapter : IOrbitrapInstrument
    {
        private readonly IFusionInstrumentAccess _realIapi;
        
        public event EventHandler<OrbitrapScanEventArgs> Ms1ScanArrived;
        public event EventHandler<OrbitrapScanEventArgs> Ms2ScanArrived;
        
        public RealOrbitrapAdapter(IFusionInstrumentAccess realIapi)
        {
            _realIapi = realIapi;
        }
        
        public void StartAcquisition()
        {
            // Subscribe to real Thermo IAPI events
            var ms1Container = _realIapi.GetMsScanContainer(0);
            var ms2Container = _realIapi.GetMsScanContainer(1);
            
            ms1Container.MsScanArrived += (s, e) =>
            {
                // Convert real Thermo MsScan → IOrbitrapScan
                var scan = new RealScanAdapter(e.GetScan());
                Ms1ScanArrived?.Invoke(this, new OrbitrapScanEventArgs(scan));
            };
            
            ms2Container.MsScanArrived += (s, e) =>
            {
                var scan = new RealScanAdapter(e.GetScan());
                Ms2ScanArrived?.Invoke(this, new OrbitrapScanEventArgs(scan));
            };
            
            _realIapi.StartAcquisition();
        }
    }
    
    /// <summary>
    /// Adapter: real Thermo MsScan → IOrbitrapScan
    /// </summary>
    public class RealScanAdapter : IOrbitrapScan
    {
        private readonly dynamic _thermoScan;  // Real MsScan from Thermo DLL
        
        public int ScanNumber => (int)_thermoScan.ScanNumber;
        public int MsOrder => (int)_thermoScan.MsOrder;
        public double RetentionTime => _thermoScan.RetentionTime / 60.0;  // seconds to minutes
        
        public double[] MzValues => _thermoScan.MzValues ?? Array.Empty<double>();
        public double[] IntensityValues => _thermoScan.IntensityValues ?? Array.Empty<double>();
        public int PeakCount => MzValues.Length;
        
        public double BasePeakMz => _thermoScan.BasePeakMz;
        public double BasePeakIntensity => _thermoScan.BasePeakIntensity;
        public double TotalIonCurrent => _thermoScan.TotalIonCurrent;
        
        public double? PrecursorMass => _thermoScan.MsOrder == 2 ? (double?)_thermoScan.PrecursorMass : null;
        public int? PrecursorCharge => _thermoScan.MsOrder == 2 ? (int?)_thermoScan.PrecursorCharge : null;
        public double? PrecursorIntensity => _thermoScan.MsOrder == 2 ? (double?)_thermoScan.PrecursorIntensity : null;
        public double? IsolationWidth => _thermoScan.MsOrder == 2 ? (double?)_thermoScan.IsolationWidth : null;
        
        public string Analyzer => _thermoScan.Analyzer ?? "Orbitrap";
        public double ResolutionAtMz200 => _thermoScan.Resolution;
        public double MassAccuracyPpm => _thermoScan.MassAccuracy;
        
        public IReadOnlyDictionary<string, string> TrailerExtra 
            => new Dictionary<string, string>(_thermoScan.TrailerExtraValues ?? new Dictionary<string, string>());
    }
}

// IMPLEMENTATION B: Your Mock IAPI
namespace ThermoFisher.CommonCore.Data.Business.Mock
{
    public class MockOrbitrapAdapter : IOrbitrapInstrument
    {
        private readonly IMockInstrumentAccess _mockIapi;
        
        public event EventHandler<OrbitrapScanEventArgs> Ms1ScanArrived;
        public event EventHandler<OrbitrapScanEventArgs> Ms2ScanArrived;
        
        public MockOrbitrapAdapter(IMockInstrumentAccess mockIapi)
        {
            _mockIapi = mockIapi;
        }
        
        public void StartAcquisition()
        {
            // Subscribe to mock events
            var ms1Container = _mockIapi.GetMsScanContainer(0);
            var ms2Container = _mockIapi.GetMsScanContainer(1);
            
            ms1Container.MsScanArrived += (s, e) =>
            {
                // Convert MockMsScan → IOrbitrapScan
                var scan = new MockScanAdapter(e.GetScan());
                Ms1ScanArrived?.Invoke(this, new OrbitrapScanEventArgs(scan));
            };
            
            ms2Container.MsScanArrived += (s, e) =>
            {
                var scan = new MockScanAdapter(e.GetScan());
                Ms2ScanArrived?.Invoke(this, new OrbitrapScanEventArgs(scan));
            };
            
            _mockIapi.StartAcquisition();
        }
    }
    
    /// <summary>
    /// Adapter: MockMsScan → IOrbitrapScan
    /// </summary>
    public class MockScanAdapter : IOrbitrapScan
    {
        private readonly MockMsScan _mockScan;
        
        public MockScanAdapter(MockMsScan mockScan) => _mockScan = mockScan;
        
        public int ScanNumber => _mockScan.ScanNumber;
        public int MsOrder => _mockScan.MsOrder;
        public double RetentionTime => _mockScan.RetentionTime;
        
        public double[] MzValues => _mockScan.MzValues;
        public double[] IntensityValues => _mockScan.IntensityValues;
        public int PeakCount => _mockScan.PeakCount;
        
        public double BasePeakMz => _mockScan.BasePeakMz;
        public double BasePeakIntensity => _mockScan.BasePeakIntensity;
        public double TotalIonCurrent => _mockScan.TotalIonCurrent;
        
        public double? PrecursorMass => _mockScan.PrecursorMass;
        public int? PrecursorCharge => _mockScan.PrecursorCharge;
        public double? PrecursorIntensity => _mockScan.PrecursorIntensity;
        public double? IsolationWidth => _mockScan.IsolationWidth;
        
        public string Analyzer => _mockScan.Analyzer;
        public double ResolutionAtMz200 => _mockScan.ResolutionAtMz200;
        public double MassAccuracyPpm => _mockScan.MassAccuracyPpm;
        
        public IReadOnlyDictionary<string, string> TrailerExtra 
            => _mockScan.TrailerExtraValues;
    }
}
```

---

## 🎯 THE MAGIC: Factory Pattern

Your entire downstream system only knows about `IOrbitrapInstrument`. It doesn't care if it's real or mock:

```csharp
namespace YourApplication.Proteomics
{
    public class InstrumentFactory
    {
        /// <summary>
        /// Create either real or mock instrument based on connection string.
        /// Downstream code calls this ONCE at startup, then works with IOrbitrapInstrument.
        /// </summary>
        public static IOrbitrapInstrument Create(string connectionString)
        {
            if (connectionString.StartsWith("mock://", StringComparison.OrdinalIgnoreCase))
            {
                // Development: use mock simulator
                var mockFactory = MockInstrumentFactory.Create(connectionString);
                return new MockOrbitrapAdapter(mockFactory);
            }
            else
            {
                // Production: use real Thermo IAPI
                var realIapi = ConnectToRealOrbitrap(connectionString);
                return new RealOrbitrapAdapter(realIapi);
            }
        }
    }
    
    /// <summary>
    /// Your analysis pipeline: works identically for real or mock.
    /// </summary>
    public class MsgfIntegration
    {
        public async Task<string> SearchScans(
            IOrbitrapInstrument instrument,  // Real or mock, doesn't matter
            string fastaPath)
        {
            var mzmlWriter = new MzMLExporter("scans.mzML");
            int scanCount = 0;
            
            // Subscribe to UNIFIED events
            instrument.Ms1ScanArrived += (s, e) =>
            {
                var scan = e.Scan;  // IOrbitrapScan (real or mock adapter)
                mzmlWriter.WriteScan(scan);
                Interlocked.Increment(ref scanCount);
            };
            
            instrument.Ms2ScanArrived += (s, e) =>
            {
                var scan = e.Scan;
                mzmlWriter.WriteScan(scan);
                Interlocked.Increment(ref scanCount);
            };
            
            instrument.StartAcquisition();
            
            // Wait for run to complete
            await Task.Delay(TimeSpan.FromMinutes(60));
            
            instrument.StopAcquisition();
            mzmlWriter.Close();
            
            Console.WriteLine($"Wrote {scanCount} scans to mzML");
            
            // Standard MSGF+ invocation
            return await SearchEngineIntegration.RunMsgfPlus(
                "scans.mzML",
                fastaPath,
                "results.mzid",
                "MSGFPlus.jar"
            );
        }
    }
    
    /// <summary>
    /// Entry point: shows zero difference between real and mock.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            // Use mock for development
            var instrument = InstrumentFactory.Create("mock://localhost:31417");
            
            // OR use real for production
            // var instrument = InstrumentFactory.Create("real://COM1:9600");
            
            var searcher = new MsgfIntegration();
            var results = await searcher.SearchScans(
                instrument,
                "human_proteome.fasta"
            );
            
            Console.WriteLine($"Search complete: {results}");
        }
    }
}
```

---

## 📊 DIAGRAM: Where Everything Converges

```
REAL ORBITRAP PATH                    MOCK SIMULATOR PATH
───────────────────                   ──────────────────

Hardware                              Rust LC-MS Sim
    ↓                                      ↓
Thermo IAPI DLL                       gRPC server
    ↓                                      ↓
IFusionInstrumentAccess               IMockInstrumentAccess
    ↓                                      ↓
MsScan (Thermo type)                  MockMsScan (our type)
    ↓                                      ↓
RealScanAdapter                       MockScanAdapter
    ↓                                      ↓
        ╔═════════════════════════════════╗
        ║     IOrbitrapScan (THIN WAIST)  ║  ← BOTH implementations
        ╚═════════════════════════════════╝
                      ↓
        ┌──────────────────────────────┐
        │   Your Analysis Code:        │
        │   - MSGF+ integration        │
        │   - Dashboard                │
        │   - Data processing          │
        └──────────────────────────────┘
```

---

## 🛠️ IMPLEMENTATION CHECKLIST

**Your code must implement:**

1. ✅ `IOrbitrapScan` interface (the shared contract)
2. ✅ `IOrbitrapInstrument` interface (unified entry point)
3. ✅ `RealScanAdapter` (real Thermo → IOrbitrapScan)
4. ✅ `MockScanAdapter` (MockMsScan → IOrbitrapScan)
5. ✅ `RealOrbitrapAdapter` (wires real IAPI → IOrbitrapInstrument)
6. ✅ `MockOrbitrapAdapter` (wires mock IAPI → IOrbitrapInstrument)
7. ✅ `InstrumentFactory.Create()` (returns IOrbitrapInstrument, dev/prod agnostic)

---

## 🎯 VALIDATION

**This is how you know it's correct:**

1. **Your MSGF+ integration takes ONLY `IOrbitrapInstrument` and `IOrbitrapScan`**
   - Zero references to `MockMsScan`
   - Zero references to real Thermo types
   - Zero knowledge of real vs mock

2. **Change one line in `InstrumentFactory.Create()`, everything works**
   - Mock → Real: change connection string only
   - Real → Mock: change connection string only
   - All 10,000 lines of downstream code unchanged

3. **Both paths produce identical behavior**
   - Same event sequence
   - Same scan data
   - Same mzML output
   - Same MSGF+ results

---

## 🚀 THIS IS YOUR THIN WAIST

**Everything upstream (Rust sim, C# mock factory) → IOrbitrapScan**
**Everything downstream (MSGF+, analysis, dashboards) ← IOrbitrapScan**

**Nothing else crosses this boundary.**

The mock simulator's job is ONLY to produce `IOrbitrapScan` objects at the right timing and with realistic data. The real instrument's job is the same. Your analysis code never knows which it got.

This is enterprise architecture at its finest.
