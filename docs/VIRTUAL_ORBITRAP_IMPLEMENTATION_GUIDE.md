# Virtual Orbitrap IAPI Implementation Guide

## Executive Summary

This document provides the complete technical specification for building a virtual Orbitrap mass spectrometer that emits an authentic IAPI (Instrument Application Programming Interface) stream. The implementation transforms simulated proteomics data from MaSS-Simulator into .NET objects indistinguishable from real Thermo Orbitrap instrument output.

**Target Pipeline:**
```
FASTA → MaSS-Simulator (w/ PTM) → mzML → C# Loader → .NET Scan Objects → IAPI Events
```

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Complete .NET Schema Definitions](#2-complete-net-schema-definitions)
3. [Gap Analysis: MaSS-Simulator vs IAPI](#3-gap-analysis-mass-simulator-vs-iapi)
4. [MVP Scope (v1.0)](#4-mvp-scope-v10)
5. [Enrichment Layer Implementation](#5-enrichment-layer-implementation)
6. [Object Builders](#6-object-builders)
7. [IAPI Event Emitter](#7-iapi-event-emitter)
8. [Build Order & Dependencies](#8-build-order--dependencies)
9. [Testing Strategy](#9-testing-strategy)
10. [Future Enhancements (v1.1+)](#10-future-enhancements-v11)

---

## 1. Architecture Overview

### 1.1 System Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           VIRTUAL ORBITRAP SIMULATOR                         │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────────┐
│   FASTA     │────▶│ MaSS-Sim    │────▶│  .ms2 file  │────▶│ msconvert       │
│   + PTMs    │     │ (Java)      │     │             │     │ (ProteoWizard)  │
└─────────────┘     └─────────────┘     └─────────────┘     └────────┬────────┘
                                                                      │
                                                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              C# CORE LIBRARY                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐                                                        │
│  │  mzML Parser    │◀────────────────── .mzML input                         │
│  │  (System.Xml)   │                                                        │
│  └────────┬────────┘                                                        │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     ENRICHMENT LAYER                                 │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐               │    │
│  │  │ Resolution   │  │ Noise        │  │ Baseline     │               │    │
│  │  │ Calculator   │  │ Synthesizer  │  │ Generator    │               │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘               │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐               │    │
│  │  │ Filter String│  │ Trailer Extra│  │ Scan Stats   │               │    │
│  │  │ Generator    │  │ Synthesizer  │  │ Calculator   │               │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘               │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     OBJECT FACTORY                                   │    │
│  │  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐   │    │
│  │  │ CentroidStream   │  │ clsScanInfo      │  │ ScanStatistics   │   │    │
│  │  │ Builder          │  │ Builder          │  │ Builder          │   │    │
│  │  └──────────────────┘  └──────────────────┘  └──────────────────┘   │    │
│  │  ┌──────────────────┐  ┌──────────────────┐                         │    │
│  │  │ RawFileInfo      │  │ DeviceInfo       │                         │    │
│  │  │ Builder          │  │ Builder          │                         │    │
│  │  └──────────────────┘  └──────────────────┘                         │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     IAPI EVENT EMITTER                               │    │
│  │  • Implements IVirtualRawData (custom interface)                     │    │
│  │  • GetCentroidStream(scanNumber) → CentroidStream                    │    │
│  │  • GetScanInfo(scanNumber) → clsScanInfo                             │    │
│  │  • GetScanStatistics(scanNumber) → ScanStatistics                    │    │
│  │  • Raises OnScanArrived events with configurable timing              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
                         ┌──────────────────────────────┐
                         │   DOWNSTREAM CONSUMERS       │
                         │   (Your existing pipeline)   │
                         └──────────────────────────────┘
```

### 1.2 Component Responsibilities

| Component | Responsibility | Input | Output |
|-----------|---------------|-------|--------|
| MaSS-Simulator | Generate MS/MS spectra from FASTA | FASTA + PTM config | .ms2 files |
| msconvert | Format conversion | .ms2 | .mzML |
| mzML Parser | Extract peak data & metadata | .mzML | Raw peak arrays |
| Enrichment Layer | Add Orbitrap-realistic parameters | Raw peaks | Enriched peaks |
| Object Factory | Construct .NET IAPI objects | Enriched data | .NET objects |
| IAPI Emitter | Expose data via IAPI-compatible interface | .NET objects | Events/Methods |

---

## 2. Complete .NET Schema Definitions

### 2.1 CentroidStream (Primary Orbitrap Peak Data)

This is the **most critical structure** for Orbitrap data. FTMS instruments store centroid peaks with rich metadata.

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// Represents centroid peak data from an Orbitrap scan.
    /// All arrays are parallel (same index = same peak).
    /// Mirrors ThermoFisher.CommonCore.Data.Business.CentroidStream
    /// </summary>
    public class CentroidStream
    {
        //=================================================================
        // CORE PEAK DATA (Required for MVP)
        //=================================================================

        /// <summary>
        /// Mass-to-charge ratios for each centroid peak.
        /// Values are observed m/z (NOT monoisotopic mass).
        /// Typical range: 100-2000 m/z for proteomics.
        /// </summary>
        public double[] Masses { get; set; }

        /// <summary>
        /// Intensity values for each peak.
        /// Units: arbitrary (counts or normalized).
        /// Dynamic range typically 1e2 to 1e10.
        /// </summary>
        public double[] Intensities { get; set; }

        /// <summary>
        /// Number of centroid peaks in this scan.
        /// Must equal length of all parallel arrays.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Scan number this data belongs to.
        /// 1-indexed (first scan = 1).
        /// </summary>
        public int ScanNumber { get; set; }

        //=================================================================
        // ORBITRAP-SPECIFIC METADATA (Required for MVP)
        //=================================================================

        /// <summary>
        /// Mass resolution at each peak position.
        /// Orbitrap resolution formula: R(m) = R0 * sqrt(m0/m)
        /// Typical values: 15,000 - 500,000 depending on settings.
        /// R0 = resolution at reference mass m0 (usually 200 m/z).
        /// </summary>
        public double[] Resolutions { get; set; }

        /// <summary>
        /// Noise floor estimate at each peak position.
        /// Used for signal-to-noise calculation: S/N = Intensity / Noise
        /// Typical values: 10-1000 (instrument dependent).
        /// </summary>
        public double[] Noises { get; set; }

        /// <summary>
        /// Baseline intensity at each peak position.
        /// Represents chemical/electronic background.
        /// Typical values: 100-5000.
        /// </summary>
        public double[] Baselines { get; set; }

        //=================================================================
        // CHARGE STATE DATA (v1.1 - set to 0 for MVP)
        //=================================================================

        /// <summary>
        /// Charge state for each peak.
        /// 0 = undetermined/unknown.
        /// Positive integers for assigned charges (1, 2, 3, ...).
        /// Determined from isotope spacing or deconvolution.
        /// </summary>
        public int[] Charges { get; set; }

        //=================================================================
        // CALIBRATION DATA (Optional)
        //=================================================================

        /// <summary>
        /// Mass calibration polynomial coefficients.
        /// Used to convert raw frequency to m/z.
        /// Typically 2-5 coefficients.
        /// </summary>
        public double[] Coefficients { get; set; }

        /// <summary>
        /// Number of calibration coefficients.
        /// </summary>
        public int CoefficientsCount { get; set; }

        //=================================================================
        // PEAK FLAGS (Optional)
        //=================================================================

        /// <summary>
        /// Flags for each peak (saturated, reference, etc.).
        /// Use PeakOptions enum values.
        /// </summary>
        public PeakOptions[] Flags { get; set; }

        //=================================================================
        // COMPUTED PROPERTIES
        //=================================================================

        /// <summary>
        /// m/z of the most intense peak in the scan.
        /// </summary>
        public double BasePeakMass => GetBasePeakMass();

        /// <summary>
        /// Intensity of the most intense peak.
        /// </summary>
        public double BasePeakIntensity => GetBasePeakIntensity();

        /// <summary>
        /// Resolution at the base peak position.
        /// </summary>
        public double BasePeakResolution => GetBasePeakResolution();

        /// <summary>
        /// Noise at the base peak position.
        /// </summary>
        public double BasePeakNoise => GetBasePeakNoise();

        //=================================================================
        // HELPER METHODS
        //=================================================================

        private int _basePeakIndex = -1;

        private int FindBasePeakIndex()
        {
            if (_basePeakIndex >= 0) return _basePeakIndex;
            if (Intensities == null || Intensities.Length == 0) return -1;

            int maxIdx = 0;
            double maxInt = Intensities[0];
            for (int i = 1; i < Intensities.Length; i++)
            {
                if (Intensities[i] > maxInt)
                {
                    maxInt = Intensities[i];
                    maxIdx = i;
                }
            }
            _basePeakIndex = maxIdx;
            return maxIdx;
        }

        private double GetBasePeakMass() =>
            FindBasePeakIndex() >= 0 ? Masses[_basePeakIndex] : 0;

        private double GetBasePeakIntensity() =>
            FindBasePeakIndex() >= 0 ? Intensities[_basePeakIndex] : 0;

        private double GetBasePeakResolution() =>
            FindBasePeakIndex() >= 0 && Resolutions != null ? Resolutions[_basePeakIndex] : 0;

        private double GetBasePeakNoise() =>
            FindBasePeakIndex() >= 0 && Noises != null ? Noises[_basePeakIndex] : 0;

        /// <summary>
        /// Calculate signal-to-noise ratio for a specific peak.
        /// </summary>
        public double GetSignalToNoise(int index)
        {
            if (Noises == null || index < 0 || index >= Length) return 0;
            return Noises[index] > 0 ? Intensities[index] / Noises[index] : 0;
        }

        /// <summary>
        /// Validate that all parallel arrays have consistent length.
        /// </summary>
        public bool Validate()
        {
            if (Masses == null || Intensities == null) return false;
            if (Masses.Length != Length || Intensities.Length != Length) return false;
            if (Resolutions != null && Resolutions.Length != Length) return false;
            if (Noises != null && Noises.Length != Length) return false;
            if (Baselines != null && Baselines.Length != Length) return false;
            if (Charges != null && Charges.Length != Length) return false;
            return true;
        }
    }

    /// <summary>
    /// Peak option flags matching Thermo's PeakOptions enum.
    /// </summary>
    [Flags]
    public enum PeakOptions
    {
        None = 0,
        Saturated = 1,
        Reference = 2,
        Exception = 4,
        Fragmented = 8
    }
}
```

### 2.2 clsScanInfo (Per-Scan Metadata)

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// Complete metadata for a single scan.
    /// Mirrors ThermoRawFileReader.clsScanInfo.
    /// </summary>
    public class ScanInfo
    {
        //=================================================================
        // CORE IDENTIFIERS
        //=================================================================

        /// <summary>
        /// Scan number (1-indexed).
        /// </summary>
        public int ScanNumber { get; set; }

        /// <summary>
        /// MS acquisition level.
        /// 1 = MS1 (survey scan)
        /// 2 = MS/MS (MS2)
        /// 3 = MS3
        /// </summary>
        public int MSLevel { get; set; }

        /// <summary>
        /// Event number within a scan segment.
        /// 1 = parent scan, 2 = first fragment, etc.
        /// </summary>
        public int EventNumber { get; set; }

        //=================================================================
        // TIMING
        //=================================================================

        /// <summary>
        /// Retention time in MINUTES.
        /// Start of scan acquisition relative to run start.
        /// </summary>
        public double RetentionTime { get; set; }

        /// <summary>
        /// Ion injection time in MILLISECONDS.
        /// Time ions were accumulated in the C-trap.
        /// Typical range: 1-500 ms.
        /// </summary>
        public double IonInjectionTime { get; set; }

        //=================================================================
        // SCAN STATISTICS
        //=================================================================

        /// <summary>
        /// Number of peaks (m/z-intensity pairs) in the scan.
        /// -1 if unknown.
        /// </summary>
        public int NumPeaks { get; set; } = -1;

        /// <summary>
        /// Total Ion Current - sum of all ion intensities.
        /// </summary>
        public double TotalIonCurrent { get; set; }

        /// <summary>
        /// m/z of the most abundant peak.
        /// </summary>
        public double BasePeakMZ { get; set; }

        /// <summary>
        /// Intensity of the most abundant peak.
        /// </summary>
        public double BasePeakIntensity { get; set; }

        /// <summary>
        /// Lowest observed m/z in the scan.
        /// </summary>
        public double LowMass { get; set; }

        /// <summary>
        /// Highest observed m/z in the scan.
        /// </summary>
        public double HighMass { get; set; }

        //=================================================================
        // FILTER STRING (Critical for downstream processing)
        //=================================================================

        /// <summary>
        /// Thermo filter string describing the scan.
        /// Examples:
        ///   "FTMS + p NSI Full ms [100.00-2000.00]"
        ///   "FTMS + p NSI d Full ms2 750.50@hcd35.00 [200.00-2000.00]"
        /// </summary>
        public string FilterText { get; set; } = string.Empty;

        //=================================================================
        // PARENT ION INFO (MS2+ scans)
        //=================================================================

        /// <summary>
        /// Precursor m/z for MS2+ scans.
        /// 0 for MS1 scans.
        /// </summary>
        public double ParentIonMZ { get; set; }

        /// <summary>
        /// Monoisotopic m/z as determined by instrument.
        /// From trailer extra "Monoisotopic M/Z:" event.
        /// Null if not available.
        /// </summary>
        public double? ParentIonMonoisotopicMZ { get; set; }

        /// <summary>
        /// Scan number of the parent (precursor) scan.
        /// 0 for MS1 scans.
        /// </summary>
        public int ParentScan { get; set; }

        /// <summary>
        /// Isolation window width in m/z units.
        /// Typical values: 1.0-2.0 for DDA, 10-50 for DIA.
        /// </summary>
        public double IsolationWindowWidthMZ { get; set; }

        /// <summary>
        /// List of parent ions with fragmentation details.
        /// Multiple entries for MSn where n > 2.
        /// </summary>
        public List<ParentIonInfo> ParentIons { get; set; } = new();

        /// <summary>
        /// List of dependent (child) scan numbers.
        /// Empty for MS2+ scans.
        /// </summary>
        public List<int> DependentScans { get; set; } = new();

        //=================================================================
        // FRAGMENTATION INFO
        //=================================================================

        /// <summary>
        /// Activation/dissociation type.
        /// CID, HCD, ETD, EThcD, etc.
        /// </summary>
        public ActivationType ActivationType { get; set; } = ActivationType.Unknown;

        /// <summary>
        /// Collision mode string (lowercase).
        /// "cid", "hcd", "etd", "ethcd", "etcid"
        /// </summary>
        public string CollisionMode { get; set; } = string.Empty;

        //=================================================================
        // SCAN TYPE FLAGS
        //=================================================================

        /// <summary>
        /// True if Selected Ion Monitoring scan.
        /// </summary>
        public bool SIMScan { get; set; }

        /// <summary>
        /// Multiple Reaction Monitoring type.
        /// </summary>
        public MRMScanType MRMScanType { get; set; } = MRMScanType.NotMRM;

        /// <summary>
        /// True if zoom scan (narrow mass range).
        /// </summary>
        public bool ZoomScan { get; set; }

        /// <summary>
        /// True if Data-Independent Acquisition.
        /// Typically set when isolation width >= 6.5 m/z.
        /// </summary>
        public bool IsDIA { get; set; }

        //=================================================================
        // INSTRUMENT FLAGS
        //=================================================================

        /// <summary>
        /// Ionization polarity.
        /// </summary>
        public IonMode IonMode { get; set; } = IonMode.Unknown;

        /// <summary>
        /// True if data is centroided (stick spectrum).
        /// False if profile (continuum) data.
        /// </summary>
        public bool IsCentroided { get; set; }

        /// <summary>
        /// True if acquired on high-resolution analyzer.
        /// (Orbitrap, FTMS, TOF, Astral)
        /// </summary>
        public bool IsHighResolution { get; set; }

        //=================================================================
        // SCAN EVENTS (Key-Value Metadata)
        //=================================================================

        /// <summary>
        /// Trailer extra / scan event key-value pairs.
        /// Common keys:
        ///   "Monoisotopic M/Z:"
        ///   "Charge State:"
        ///   "MS2 Isolation Width:"
        ///   "Ion Injection Time (ms):"
        ///   "AGC Target:"
        /// </summary>
        public List<KeyValuePair<string, string>> ScanEvents { get; set; } = new();

        /// <summary>
        /// Status log key-value pairs.
        /// </summary>
        public List<KeyValuePair<string, string>> StatusLog { get; set; } = new();

        //=================================================================
        // MRM INFO (for SRM/MRM scans)
        //=================================================================

        /// <summary>
        /// MRM configuration if applicable.
        /// </summary>
        public MRMInfo MRMInfo { get; set; }

        //=================================================================
        // CONTROLLER PARAMETERS
        //=================================================================

        /// <summary>
        /// Number of channels (for multi-channel detectors).
        /// </summary>
        public int NumChannels { get; set; }

        /// <summary>
        /// True if uniform time sampling.
        /// </summary>
        public bool UniformTime { get; set; }

        /// <summary>
        /// Sampling frequency (Hz).
        /// </summary>
        public double Frequency { get; set; }

        //=================================================================
        // HELPER METHODS
        //=================================================================

        /// <summary>
        /// Try to get a scan event value by key name.
        /// </summary>
        public bool TryGetScanEvent(string eventName, out string eventValue, bool partialMatch = false)
        {
            foreach (var kvp in ScanEvents)
            {
                bool matches = partialMatch
                    ? kvp.Key.StartsWith(eventName, StringComparison.OrdinalIgnoreCase)
                    : kvp.Key.Equals(eventName, StringComparison.OrdinalIgnoreCase);

                if (matches)
                {
                    eventValue = kvp.Value;
                    return true;
                }
            }
            eventValue = string.Empty;
            return false;
        }

        /// <summary>
        /// Store scan events from parallel string arrays.
        /// </summary>
        public void StoreScanEvents(string[] names, string[] values)
        {
            ScanEvents.Clear();
            for (int i = 0; i < names.Length && i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(names[i]))
                {
                    ScanEvents.Add(new KeyValuePair<string, string>(names[i], values[i]?.Trim() ?? ""));
                }
            }
        }
    }
}
```

### 2.3 ParentIonInfo (Fragmentation Details)

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// Parent ion fragmentation information.
    /// One entry per fragmentation step (MS2 has 1, MS3 has 2, etc.)
    /// </summary>
    public struct ParentIonInfo
    {
        /// <summary>
        /// MS level of THIS spectrum (not the parent).
        /// </summary>
        public int MSLevel { get; set; }

        /// <summary>
        /// Parent ion m/z that was fragmented.
        /// </summary>
        public double ParentIonMZ { get; set; }

        /// <summary>
        /// Primary collision/activation mode.
        /// Lowercase: "cid", "hcd", "etd", "ethcd", "etcid"
        /// </summary>
        public string CollisionMode { get; set; }

        /// <summary>
        /// Secondary collision mode (for dual activation like EThcD).
        /// </summary>
        public string CollisionMode2 { get; set; }

        /// <summary>
        /// Primary collision energy (normalized or absolute).
        /// Typical range: 20-40 for HCD, 25-35 for CID.
        /// </summary>
        public float CollisionEnergy { get; set; }

        /// <summary>
        /// Secondary collision energy (for dual activation).
        /// </summary>
        public float CollisionEnergy2 { get; set; }

        /// <summary>
        /// Activation type enum value.
        /// </summary>
        public ActivationType ActivationType { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(CollisionMode))
                return $"ms{MSLevel} {ParentIonMZ:F2}";
            return $"ms{MSLevel} {ParentIonMZ:F2}@{CollisionMode}{CollisionEnergy:F2}";
        }
    }
}
```

### 2.4 Enumerations

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// Activation/dissociation types.
    /// Mirrors ThermoFisher.CommonCore.Data.FilterEnums.ActivationType
    /// </summary>
    public enum ActivationType
    {
        Unknown = -1,
        CID = 0,      // Collision-Induced Dissociation
        MPD = 1,      // Multi Photon Dissociation
        ECD = 2,      // Electron Capture Dissociation
        PQD = 3,      // Pulsed Q Dissociation
        ETD = 4,      // Electron Transfer Dissociation
        HCD = 5,      // Higher-energy Collisional Dissociation
        AnyType = 6,
        SA = 7,       // Supplemental Activation
        PTR = 8,      // Proton Transfer Reaction
        NETD = 9,     // Negative ETD
        NPTR = 10,    // Negative PTR
        UVPD = 11,    // Ultraviolet Photodissociation
        // Modes 12-37 reserved for future activation types
    }

    /// <summary>
    /// MRM/SRM scan types.
    /// </summary>
    public enum MRMScanType
    {
        NotMRM = 0,
        MRMQMS = 1,   // Multiple SIM ranges
        SRM = 2,      // Selected Reaction Monitoring
        FullNL = 3,   // Full Neutral Loss
        SIM = 4       // Selected Ion Monitoring
    }

    /// <summary>
    /// Ionization polarity.
    /// </summary>
    public enum IonMode
    {
        Unknown = 0,
        Positive = 1,
        Negative = 2
    }

    /// <summary>
    /// Mass analyzer types.
    /// </summary>
    public enum MassAnalyzer
    {
        Any,
        ITMS,     // Ion Trap
        TQMS,     // Triple Quad
        SQMS,     // Single Quad
        TOFMS,    // Time of Flight
        FTMS,     // Orbitrap / FT-ICR
        Sector,   // Magnetic Sector
        ASTMS     // Astral
    }

    /// <summary>
    /// MS order (scan power).
    /// </summary>
    public enum MSOrder
    {
        Any,
        Ms,       // MS1
        Ms2,      // MS/MS
        Ms3,      // MS3
        Ms4, Ms5, Ms6, Ms7, Ms8, Ms9, Ms10,
        ParentScan,
        ZoomScan
    }

    /// <summary>
    /// Device types in raw files.
    /// </summary>
    public enum Device
    {
        None,
        MS,           // Mass Spectrometer
        MSAnalog,
        Analog,       // Analog device (LC pumps, etc.)
        UV,           // UV detector
        PDA,          // Photo Diode Array
        Other
    }

    /// <summary>
    /// Data units for non-MS devices.
    /// </summary>
    public enum DataUnits
    {
        None,                    // Counts
        AbsorbanceUnits,
        MilliAbsorbanceUnits,
        MicroAbsorbanceUnits,
        Volts,
        MilliVolts,
        MicroVolts
    }
}
```

### 2.5 ScanStatistics

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// Scan-level statistics.
    /// Lighter weight than full ScanInfo for quick access.
    /// </summary>
    public class ScanStatistics
    {
        /// <summary>
        /// Scan number (1-indexed).
        /// </summary>
        public int ScanNumber { get; set; }

        /// <summary>
        /// Retention time in minutes.
        /// </summary>
        public double StartTime { get; set; }

        /// <summary>
        /// Total Ion Current.
        /// </summary>
        public double TIC { get; set; }

        /// <summary>
        /// Base peak intensity.
        /// </summary>
        public double BasePeakIntensity { get; set; }

        /// <summary>
        /// Base peak m/z.
        /// </summary>
        public double BasePeakMass { get; set; }

        /// <summary>
        /// Lowest m/z in scan.
        /// </summary>
        public double LowMass { get; set; }

        /// <summary>
        /// Highest m/z in scan.
        /// </summary>
        public double HighMass { get; set; }

        /// <summary>
        /// Packet type indicator (internal format).
        /// </summary>
        public int PacketType { get; set; }

        /// <summary>
        /// True if centroid data.
        /// </summary>
        public bool IsCentroidScan { get; set; }

        /// <summary>
        /// True if separate centroid stream exists (FTMS).
        /// </summary>
        public bool HasCentroidStream { get; set; }

        /// <summary>
        /// Number of channels.
        /// </summary>
        public int NumberOfChannels { get; set; }

        /// <summary>
        /// Uniform time sampling flag.
        /// </summary>
        public bool UniformTime { get; set; }

        /// <summary>
        /// Sampling frequency.
        /// </summary>
        public double Frequency { get; set; }
    }
}
```

### 2.6 RawFileInfo (File-Level Metadata)

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// File-level metadata for the virtual raw file.
    /// </summary>
    public class RawFileInfo
    {
        //=================================================================
        // SAMPLE INFO
        //=================================================================

        public string AcquisitionDate { get; set; } = string.Empty;
        public string AcquisitionFilename { get; set; } = string.Empty;
        public string Comment1 { get; set; } = string.Empty;
        public string Comment2 { get; set; } = string.Empty;
        public string SampleName { get; set; } = string.Empty;
        public string SampleComment { get; set; } = string.Empty;

        //=================================================================
        // FILE CREATION
        //=================================================================

        public DateTime CreationDate { get; set; } = DateTime.Now;
        public string CreatorID { get; set; } = Environment.UserName;
        public int VersionNumber { get; set; } = 66; // Current RAW format version

        //=================================================================
        // INSTRUMENT INFO
        //=================================================================

        /// <summary>
        /// Instrument name (e.g., "Orbitrap Exploris 480")
        /// </summary>
        public string InstName { get; set; } = "Virtual Orbitrap";

        /// <summary>
        /// Instrument model (e.g., "Orbitrap Exploris 480")
        /// </summary>
        public string InstModel { get; set; } = "Virtual Orbitrap Simulator";

        /// <summary>
        /// Serial number.
        /// </summary>
        public string InstSerialNumber { get; set; } = "SIM-001";

        /// <summary>
        /// Hardware version string.
        /// </summary>
        public string InstHardwareVersion { get; set; } = "1.0";

        /// <summary>
        /// Software version string.
        /// </summary>
        public string InstSoftwareVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Instrument flags (TIM, NLM, PIM, DDZMap).
        /// </summary>
        public string InstFlags { get; set; } = string.Empty;

        /// <summary>
        /// Additional instrument description.
        /// </summary>
        public string InstrumentDescription { get; set; } = "Virtual Orbitrap IAPI Simulator";

        //=================================================================
        // DEVICE TRACKING
        //=================================================================

        /// <summary>
        /// Devices present in the file.
        /// Key = Device type, Value = count of that device type.
        /// </summary>
        public Dictionary<Device, int> Devices { get; set; } = new()
        {
            { Device.MS, 1 }
        };

        //=================================================================
        // METHODS
        //=================================================================

        /// <summary>
        /// Instrument methods (acquisition methods).
        /// </summary>
        public List<string> InstMethods { get; set; } = new();

        /// <summary>
        /// Tune methods and their settings.
        /// </summary>
        public List<TuneMethod> TuneMethods { get; set; } = new();

        //=================================================================
        // SCAN RANGE
        //=================================================================

        /// <summary>
        /// First scan number in the file.
        /// </summary>
        public int ScanStart { get; set; } = 1;

        /// <summary>
        /// Last scan number in the file.
        /// </summary>
        public int ScanEnd { get; set; }

        /// <summary>
        /// Retention time of first scan (minutes).
        /// </summary>
        public double FirstScanTimeMinutes { get; set; } = 0.0;

        /// <summary>
        /// Retention time of last scan (minutes).
        /// </summary>
        public double LastScanTimeMinutes { get; set; }

        /// <summary>
        /// Mass resolution setting.
        /// </summary>
        public double MassResolution { get; set; } = 120000;

        //=================================================================
        // STATUS FLAGS
        //=================================================================

        public bool HasNoMSDevice { get; set; } = false;
        public bool HasNonMSDataDevice { get; set; } = false;
        public bool CorruptFile { get; set; } = false;
    }

    /// <summary>
    /// Tune method settings container.
    /// </summary>
    public class TuneMethod
    {
        public List<TuneMethodSetting> Settings { get; set; } = new();
    }

    /// <summary>
    /// Individual tune method setting.
    /// </summary>
    public struct TuneMethodSetting
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
```

### 2.7 MRMInfo

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// MRM/SRM configuration.
    /// </summary>
    public class MRMInfo
    {
        /// <summary>
        /// List of mass ranges monitored.
        /// </summary>
        public List<MRMMassRange> MRMMassList { get; set; } = new();
    }

    /// <summary>
    /// Single MRM mass range.
    /// </summary>
    public struct MRMMassRange
    {
        public double StartMass { get; set; }
        public double EndMass { get; set; }
        public double CentralMass { get; set; }

        public override string ToString() => $"{StartMass:F3}-{EndMass:F3}";
    }
}
```

### 2.8 FTLabelInfoType (High-Resolution Label Data)

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// High-resolution peak label data.
    /// Alternative representation to CentroidStream for some APIs.
    /// </summary>
    public struct FTLabelInfo
    {
        /// <summary>
        /// Observed m/z (NOT monoisotopic).
        /// </summary>
        public double Mass { get; set; }

        /// <summary>
        /// Peak intensity.
        /// </summary>
        public double Intensity { get; set; }

        /// <summary>
        /// Mass resolution at this peak.
        /// </summary>
        public float Resolution { get; set; }

        /// <summary>
        /// Baseline intensity.
        /// </summary>
        public float Baseline { get; set; }

        /// <summary>
        /// Noise floor.
        /// </summary>
        public float Noise { get; set; }

        /// <summary>
        /// Charge state (0 if undetermined).
        /// </summary>
        public int Charge { get; set; }

        /// <summary>
        /// Signal-to-noise ratio.
        /// </summary>
        public double SignalToNoise => Noise > 0 ? Intensity / Noise : 0;
    }
}
```

### 2.9 MassPrecisionInfo

```csharp
namespace VirtualOrbitrap.Schema
{
    /// <summary>
    /// Mass precision/accuracy information.
    /// </summary>
    public struct MassPrecisionInfo
    {
        /// <summary>
        /// m/z value.
        /// </summary>
        public double Mass { get; set; }

        /// <summary>
        /// Peak intensity.
        /// </summary>
        public double Intensity { get; set; }

        /// <summary>
        /// Mass accuracy in millimass units.
        /// </summary>
        public double AccuracyMMU { get; set; }

        /// <summary>
        /// Mass accuracy in parts per million.
        /// </summary>
        public double AccuracyPPM { get; set; }

        /// <summary>
        /// Mass resolution.
        /// </summary>
        public double Resolution { get; set; }
    }
}
```

---

## 3. Gap Analysis: MaSS-Simulator vs IAPI

### 3.1 Data Flow Mapping

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    DATA AVAILABILITY MATRIX                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  IAPI Field              │ MaSS-Sim │ mzML │ Need to Synthesize              │
│  ────────────────────────┼──────────┼──────┼──────────────────────           │
│  Masses[]                │    ✅    │  ✅  │  No                             │
│  Intensities[]           │    ✅    │  ✅  │  No                             │
│  MS Level                │    ✅    │  ✅  │  No                             │
│  Precursor m/z           │    ✅    │  ✅  │  No                             │
│  Charge (precursor)      │    ✅    │  ✅  │  No                             │
│  Collision Energy        │    ✅    │  ✅  │  No                             │
│  Retention Time          │    ⚠️    │  ✅  │  May need adjustment            │
│  ────────────────────────┼──────────┼──────┼──────────────────────           │
│  Resolutions[]           │    ❌    │  ❌  │  YES - MVP CRITICAL             │
│  Noises[]                │    ❌    │  ❌  │  YES - MVP CRITICAL             │
│  Baselines[]             │    ❌    │  ❌  │  YES - MVP CRITICAL             │
│  Filter String           │    ❌    │  ⚠️  │  YES - MVP CRITICAL             │
│  ────────────────────────┼──────────┼──────┼──────────────────────           │
│  Charges[] (per peak)    │    ❌    │  ❌  │  v1.1 (set to 0 for MVP)        │
│  Isotope envelopes       │    ❌    │  ❌  │  v1.1 (defer)                   │
│  Trailer Extra Events    │    ❌    │  ⚠️  │  Nice to have                   │
│  Ion Injection Time      │    ❌    │  ❌  │  Nice to have                   │
│  AGC Target              │    ❌    │  ❌  │  Nice to have                   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 MVP Scope Decision

**INCLUDED in v1.0 MVP:**
- Resolution calculation (Orbitrap physics model)
- Noise synthesis (shot noise model)
- Baseline synthesis (instrument model)
- Filter string generation
- Core object builders (CentroidStream, ScanInfo, ScanStatistics, RawFileInfo)
- Basic IAPI emitter interface

**DEFERRED to v1.1:**
- Per-peak charge state estimation
- Isotope envelope generation
- Mass precision/accuracy modeling
- Full trailer extra event synthesis

---

## 4. MVP Scope (v1.0)

### 4.1 MVP Feature List

| Feature | Priority | LOC Estimate | Complexity |
|---------|----------|--------------|------------|
| mzML Parser | P0 | ~200 | Low |
| Resolution Calculator | P0 | ~20 | Low |
| Noise Synthesizer | P0 | ~30 | Low |
| Baseline Generator | P0 | ~20 | Low |
| Filter String Generator | P0 | ~80 | Medium |
| CentroidStream Builder | P0 | ~100 | Low |
| ScanInfo Builder | P0 | ~150 | Medium |
| ScanStatistics Builder | P0 | ~50 | Low |
| RawFileInfo Builder | P0 | ~80 | Low |
| IVirtualRawData Interface | P0 | ~100 | Medium |
| **TOTAL MVP** | | **~830 LOC** | |

### 4.2 MVP Success Criteria

1. ✅ Load .mzML file from MaSS-Simulator output
2. ✅ Generate CentroidStream with realistic resolution/noise/baseline
3. ✅ Generate valid Thermo filter strings
4. ✅ Expose data via IVirtualRawData interface
5. ✅ Downstream consumer can call GetCentroidStream(scanNumber)
6. ✅ Downstream consumer can call GetScanInfo(scanNumber)

---

## 5. Enrichment Layer Implementation

### 5.1 Resolution Calculator

```csharp
namespace VirtualOrbitrap.Enrichment
{
    /// <summary>
    /// Calculates Orbitrap resolution using physics-based model.
    /// Orbitrap resolution decreases with sqrt of m/z.
    /// </summary>
    public static class ResolutionCalculator
    {
        /// <summary>
        /// Default resolution at reference mass (m/z 200).
        /// Common Orbitrap settings: 15K, 30K, 60K, 120K, 240K, 480K
        /// </summary>
        public const double DefaultR0 = 120000;

        /// <summary>
        /// Default reference mass for resolution specification.
        /// Thermo specifies resolution at m/z 200.
        /// </summary>
        public const double DefaultM0 = 200.0;

        /// <summary>
        /// Calculate resolution for an array of m/z values.
        /// Formula: R(m) = R0 * sqrt(m0 / m)
        /// </summary>
        /// <param name="masses">Array of m/z values</param>
        /// <param name="r0">Resolution at reference mass (default: 120,000)</param>
        /// <param name="m0">Reference mass (default: 200 m/z)</param>
        /// <returns>Array of resolution values</returns>
        public static double[] Calculate(double[] masses, double r0 = DefaultR0, double m0 = DefaultM0)
        {
            if (masses == null || masses.Length == 0)
                return Array.Empty<double>();

            var resolutions = new double[masses.Length];
            for (int i = 0; i < masses.Length; i++)
            {
                resolutions[i] = CalculateSingle(masses[i], r0, m0);
            }
            return resolutions;
        }

        /// <summary>
        /// Calculate resolution for a single m/z value.
        /// </summary>
        public static double CalculateSingle(double mass, double r0 = DefaultR0, double m0 = DefaultM0)
        {
            if (mass <= 0) return 0;
            return r0 * Math.Sqrt(m0 / mass);
        }

        /// <summary>
        /// Get resolution setting string for common Orbitrap configurations.
        /// </summary>
        public static string GetResolutionSettingName(double r0)
        {
            return r0 switch
            {
                >= 450000 => "480K",
                >= 200000 => "240K",
                >= 100000 => "120K",
                >= 50000 => "60K",
                >= 25000 => "30K",
                >= 12000 => "15K",
                _ => $"{r0 / 1000:F0}K"
            };
        }
    }
}
```

### 5.2 Noise Synthesizer

```csharp
namespace VirtualOrbitrap.Enrichment
{
    /// <summary>
    /// Generates realistic noise values for Orbitrap peaks.
    /// Model: noise = sqrt(intensity) * shotFactor + electronicNoise
    /// </summary>
    public class NoiseSynthesizer
    {
        private readonly Random _rng;

        /// <summary>
        /// Shot noise contribution factor.
        /// Higher = more noise relative to signal.
        /// Typical range: 0.01 - 0.05
        /// </summary>
        public double ShotNoiseFactor { get; set; } = 0.02;

        /// <summary>
        /// Electronic/baseline noise floor.
        /// Typical range: 50 - 500 counts.
        /// </summary>
        public double ElectronicNoiseFloor { get; set; } = 100;

        /// <summary>
        /// Random variation in electronic noise (0-1).
        /// </summary>
        public double NoiseVariance { get; set; } = 0.3;

        public NoiseSynthesizer(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generate noise values for an intensity array.
        /// </summary>
        public double[] Generate(double[] intensities)
        {
            if (intensities == null || intensities.Length == 0)
                return Array.Empty<double>();

            var noises = new double[intensities.Length];
            for (int i = 0; i < intensities.Length; i++)
            {
                noises[i] = GenerateSingle(intensities[i]);
            }
            return noises;
        }

        /// <summary>
        /// Generate noise for a single intensity value.
        /// </summary>
        public double GenerateSingle(double intensity)
        {
            // Shot noise component: proportional to sqrt(intensity)
            double shotNoise = Math.Sqrt(Math.Abs(intensity)) * ShotNoiseFactor;

            // Electronic noise component: baseline with random variation
            double variation = 1.0 + ((_rng.NextDouble() - 0.5) * 2 * NoiseVariance);
            double electronicNoise = ElectronicNoiseFloor * variation;

            // Total noise (RSS combination)
            return Math.Sqrt(shotNoise * shotNoise + electronicNoise * electronicNoise);
        }
    }
}
```

### 5.3 Baseline Generator

```csharp
namespace VirtualOrbitrap.Enrichment
{
    /// <summary>
    /// Generates baseline intensity values.
    /// Model: slowly varying function with random fluctuations.
    /// </summary>
    public class BaselineGenerator
    {
        private readonly Random _rng;

        /// <summary>
        /// Mean baseline level.
        /// Typical range: 100 - 1000 counts.
        /// </summary>
        public double MeanBaseline { get; set; } = 500;

        /// <summary>
        /// Baseline variance (absolute, not percentage).
        /// </summary>
        public double Variance { get; set; } = 100;

        /// <summary>
        /// Drift rate per peak (for slowly varying baseline).
        /// Set to 0 for flat baseline.
        /// </summary>
        public double DriftRate { get; set; } = 0;

        public BaselineGenerator(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generate baseline values for a scan.
        /// </summary>
        public double[] Generate(int length)
        {
            if (length <= 0)
                return Array.Empty<double>();

            var baselines = new double[length];
            double currentLevel = MeanBaseline;

            for (int i = 0; i < length; i++)
            {
                // Add random fluctuation
                double fluctuation = (_rng.NextDouble() - 0.5) * 2 * Variance;
                baselines[i] = Math.Max(0, currentLevel + fluctuation);

                // Apply drift
                currentLevel += DriftRate;
            }

            return baselines;
        }
    }
}
```

### 5.4 Filter String Generator

```csharp
namespace VirtualOrbitrap.Enrichment
{
    /// <summary>
    /// Generates authentic Thermo filter strings.
    /// </summary>
    public static class FilterStringGenerator
    {
        /// <summary>
        /// Generate a complete filter string.
        /// </summary>
        /// <param name="msLevel">1 for MS1, 2 for MS2, etc.</param>
        /// <param name="polarity">IonMode.Positive or IonMode.Negative</param>
        /// <param name="isCentroid">True for centroid, false for profile</param>
        /// <param name="massAnalyzer">MassAnalyzer.FTMS for Orbitrap</param>
        /// <param name="precursorMz">Precursor m/z for MS2+ (ignored for MS1)</param>
        /// <param name="activationType">HCD, CID, ETD, etc.</param>
        /// <param name="collisionEnergy">Collision energy value</param>
        /// <param name="lowMass">Scan range low m/z</param>
        /// <param name="highMass">Scan range high m/z</param>
        /// <param name="isDataDependent">True for DDA scans</param>
        /// <param name="ionizationMode">NSI, ESI, APCI, etc.</param>
        /// <returns>Thermo-format filter string</returns>
        public static string Generate(
            int msLevel,
            IonMode polarity = IonMode.Positive,
            bool isCentroid = true,
            MassAnalyzer massAnalyzer = MassAnalyzer.FTMS,
            double precursorMz = 0,
            ActivationType activationType = ActivationType.HCD,
            double collisionEnergy = 30,
            double lowMass = 100,
            double highMass = 2000,
            bool isDataDependent = true,
            string ionizationMode = "NSI")
        {
            var sb = new StringBuilder();

            // Mass analyzer
            sb.Append(GetAnalyzerString(massAnalyzer));
            sb.Append(' ');

            // Polarity
            sb.Append(polarity == IonMode.Positive ? '+' : '-');
            sb.Append(' ');

            // Profile/Centroid
            sb.Append(isCentroid ? 'c' : 'p');
            sb.Append(' ');

            // Ionization mode
            sb.Append(ionizationMode);
            sb.Append(' ');

            // Data dependent flag (for MS2+)
            if (msLevel > 1 && isDataDependent)
            {
                sb.Append("d ");
            }

            // Scan type
            sb.Append("Full ");

            // MS order
            if (msLevel == 1)
            {
                sb.Append("ms ");
            }
            else
            {
                sb.Append($"ms{msLevel} ");

                // Precursor and activation
                sb.Append($"{precursorMz:F2}@{GetActivationString(activationType)}{collisionEnergy:F2} ");
            }

            // Mass range
            sb.Append($"[{lowMass:F2}-{highMass:F2}]");

            return sb.ToString();
        }

        /// <summary>
        /// Generate filter string for MS1 scan.
        /// </summary>
        public static string GenerateMS1(
            IonMode polarity = IonMode.Positive,
            bool isCentroid = true,
            double lowMass = 100,
            double highMass = 2000)
        {
            return Generate(1, polarity, isCentroid, MassAnalyzer.FTMS,
                0, ActivationType.Unknown, 0, lowMass, highMass, false);
        }

        /// <summary>
        /// Generate filter string for MS2 scan.
        /// </summary>
        public static string GenerateMS2(
            double precursorMz,
            ActivationType activationType = ActivationType.HCD,
            double collisionEnergy = 30,
            IonMode polarity = IonMode.Positive,
            bool isCentroid = true,
            double lowMass = 100,
            double highMass = 2000,
            bool isDataDependent = true)
        {
            return Generate(2, polarity, isCentroid, MassAnalyzer.FTMS,
                precursorMz, activationType, collisionEnergy, lowMass, highMass, isDataDependent);
        }

        private static string GetAnalyzerString(MassAnalyzer analyzer)
        {
            return analyzer switch
            {
                MassAnalyzer.FTMS => "FTMS",
                MassAnalyzer.ITMS => "ITMS",
                MassAnalyzer.TOFMS => "TOFMS",
                MassAnalyzer.ASTMS => "ASTMS",
                MassAnalyzer.TQMS => "TQMS",
                MassAnalyzer.SQMS => "SQMS",
                _ => "FTMS"
            };
        }

        private static string GetActivationString(ActivationType activation)
        {
            return activation switch
            {
                ActivationType.HCD => "hcd",
                ActivationType.CID => "cid",
                ActivationType.ETD => "etd",
                ActivationType.ECD => "ecd",
                ActivationType.PQD => "pqd",
                ActivationType.MPD => "mpd",
                ActivationType.UVPD => "uvpd",
                _ => "hcd"
            };
        }

        /// <summary>
        /// Parse an activation type from a filter string fragment.
        /// </summary>
        public static ActivationType ParseActivationType(string fragment)
        {
            var lower = fragment.ToLowerInvariant();
            if (lower.Contains("hcd")) return ActivationType.HCD;
            if (lower.Contains("cid")) return ActivationType.CID;
            if (lower.Contains("etd")) return ActivationType.ETD;
            if (lower.Contains("ecd")) return ActivationType.ECD;
            if (lower.Contains("pqd")) return ActivationType.PQD;
            if (lower.Contains("uvpd")) return ActivationType.UVPD;
            return ActivationType.Unknown;
        }
    }
}
```

### 5.5 Scan Statistics Calculator

```csharp
namespace VirtualOrbitrap.Enrichment
{
    /// <summary>
    /// Calculates scan-level statistics from peak data.
    /// </summary>
    public static class ScanStatsCalculator
    {
        /// <summary>
        /// Calculate statistics from a CentroidStream.
        /// </summary>
        public static ScanStatistics Calculate(CentroidStream stream, double retentionTime)
        {
            var stats = new ScanStatistics
            {
                ScanNumber = stream.ScanNumber,
                StartTime = retentionTime,
                IsCentroidScan = true,
                HasCentroidStream = true
            };

            if (stream.Masses == null || stream.Masses.Length == 0)
                return stats;

            // Calculate TIC
            stats.TIC = stream.Intensities.Sum();

            // Find base peak
            int maxIdx = 0;
            double maxInt = stream.Intensities[0];
            for (int i = 1; i < stream.Intensities.Length; i++)
            {
                if (stream.Intensities[i] > maxInt)
                {
                    maxInt = stream.Intensities[i];
                    maxIdx = i;
                }
            }
            stats.BasePeakIntensity = maxInt;
            stats.BasePeakMass = stream.Masses[maxIdx];

            // Mass range
            stats.LowMass = stream.Masses.Min();
            stats.HighMass = stream.Masses.Max();

            return stats;
        }

        /// <summary>
        /// Calculate statistics from raw arrays.
        /// </summary>
        public static ScanStatistics Calculate(
            int scanNumber,
            double[] masses,
            double[] intensities,
            double retentionTime)
        {
            var stats = new ScanStatistics
            {
                ScanNumber = scanNumber,
                StartTime = retentionTime,
                IsCentroidScan = true,
                HasCentroidStream = true
            };

            if (masses == null || masses.Length == 0)
                return stats;

            stats.TIC = intensities.Sum();

            int maxIdx = 0;
            double maxInt = intensities[0];
            for (int i = 1; i < intensities.Length; i++)
            {
                if (intensities[i] > maxInt)
                {
                    maxInt = intensities[i];
                    maxIdx = i;
                }
            }
            stats.BasePeakIntensity = maxInt;
            stats.BasePeakMass = masses[maxIdx];
            stats.LowMass = masses.Min();
            stats.HighMass = masses.Max();

            return stats;
        }
    }
}
```

---

## 6. Object Builders

### 6.1 CentroidStream Builder

```csharp
namespace VirtualOrbitrap.Builders
{
    /// <summary>
    /// Fluent builder for CentroidStream objects.
    /// </summary>
    public class CentroidStreamBuilder
    {
        private readonly CentroidStream _stream = new();
        private readonly ResolutionCalculator _resCalc;
        private readonly NoiseSynthesizer _noiseSynth;
        private readonly BaselineGenerator _baselineGen;

        public CentroidStreamBuilder(
            double resolutionR0 = 120000,
            int? randomSeed = null)
        {
            _noiseSynth = new NoiseSynthesizer(randomSeed);
            _baselineGen = new BaselineGenerator(randomSeed);
        }

        /// <summary>
        /// Set the scan number.
        /// </summary>
        public CentroidStreamBuilder WithScanNumber(int scanNumber)
        {
            _stream.ScanNumber = scanNumber;
            return this;
        }

        /// <summary>
        /// Set the peak data (masses and intensities).
        /// </summary>
        public CentroidStreamBuilder WithPeaks(double[] masses, double[] intensities)
        {
            if (masses == null || intensities == null)
                throw new ArgumentNullException("Masses and intensities cannot be null");
            if (masses.Length != intensities.Length)
                throw new ArgumentException("Masses and intensities must have same length");

            _stream.Masses = masses;
            _stream.Intensities = intensities;
            _stream.Length = masses.Length;
            return this;
        }

        /// <summary>
        /// Calculate and set resolution values using Orbitrap physics model.
        /// </summary>
        public CentroidStreamBuilder WithCalculatedResolutions(double r0 = 120000, double m0 = 200)
        {
            if (_stream.Masses == null)
                throw new InvalidOperationException("Must set peaks before calculating resolutions");

            _stream.Resolutions = ResolutionCalculator.Calculate(_stream.Masses, r0, m0);
            return this;
        }

        /// <summary>
        /// Set explicit resolution values.
        /// </summary>
        public CentroidStreamBuilder WithResolutions(double[] resolutions)
        {
            _stream.Resolutions = resolutions;
            return this;
        }

        /// <summary>
        /// Generate and set noise values.
        /// </summary>
        public CentroidStreamBuilder WithSynthesizedNoise(
            double shotFactor = 0.02,
            double electronicFloor = 100)
        {
            if (_stream.Intensities == null)
                throw new InvalidOperationException("Must set peaks before synthesizing noise");

            _noiseSynth.ShotNoiseFactor = shotFactor;
            _noiseSynth.ElectronicNoiseFloor = electronicFloor;
            _stream.Noises = _noiseSynth.Generate(_stream.Intensities);
            return this;
        }

        /// <summary>
        /// Set explicit noise values.
        /// </summary>
        public CentroidStreamBuilder WithNoises(double[] noises)
        {
            _stream.Noises = noises;
            return this;
        }

        /// <summary>
        /// Generate and set baseline values.
        /// </summary>
        public CentroidStreamBuilder WithSynthesizedBaseline(
            double meanLevel = 500,
            double variance = 100)
        {
            if (_stream.Length <= 0)
                throw new InvalidOperationException("Must set peaks before synthesizing baseline");

            _baselineGen.MeanBaseline = meanLevel;
            _baselineGen.Variance = variance;
            _stream.Baselines = _baselineGen.Generate(_stream.Length);
            return this;
        }

        /// <summary>
        /// Set explicit baseline values.
        /// </summary>
        public CentroidStreamBuilder WithBaselines(double[] baselines)
        {
            _stream.Baselines = baselines;
            return this;
        }

        /// <summary>
        /// Set charge states (use 0 for undetermined).
        /// </summary>
        public CentroidStreamBuilder WithCharges(int[] charges)
        {
            _stream.Charges = charges;
            return this;
        }

        /// <summary>
        /// Set all charge states to 0 (undetermined).
        /// Use this for MVP when charge estimation is deferred.
        /// </summary>
        public CentroidStreamBuilder WithUndeterminedCharges()
        {
            if (_stream.Length <= 0)
                throw new InvalidOperationException("Must set peaks before setting charges");

            _stream.Charges = new int[_stream.Length];
            return this;
        }

        /// <summary>
        /// Set calibration coefficients.
        /// </summary>
        public CentroidStreamBuilder WithCalibration(double[] coefficients)
        {
            _stream.Coefficients = coefficients;
            _stream.CoefficientsCount = coefficients?.Length ?? 0;
            return this;
        }

        /// <summary>
        /// Build the CentroidStream with validation.
        /// </summary>
        public CentroidStream Build()
        {
            // Ensure required fields
            if (_stream.Masses == null || _stream.Intensities == null)
                throw new InvalidOperationException("Masses and intensities are required");

            // Generate defaults for missing fields
            if (_stream.Resolutions == null)
                WithCalculatedResolutions();
            if (_stream.Noises == null)
                WithSynthesizedNoise();
            if (_stream.Baselines == null)
                WithSynthesizedBaseline();
            if (_stream.Charges == null)
                WithUndeterminedCharges();

            // Validate
            if (!_stream.Validate())
                throw new InvalidOperationException("CentroidStream validation failed");

            return _stream;
        }
    }
}
```

### 6.2 ScanInfo Builder

```csharp
namespace VirtualOrbitrap.Builders
{
    /// <summary>
    /// Fluent builder for ScanInfo objects.
    /// </summary>
    public class ScanInfoBuilder
    {
        private readonly ScanInfo _scanInfo = new();

        public ScanInfoBuilder WithScanNumber(int scanNumber)
        {
            _scanInfo.ScanNumber = scanNumber;
            return this;
        }

        public ScanInfoBuilder WithMSLevel(int msLevel)
        {
            _scanInfo.MSLevel = msLevel;
            return this;
        }

        public ScanInfoBuilder WithRetentionTime(double retentionTimeMinutes)
        {
            _scanInfo.RetentionTime = retentionTimeMinutes;
            return this;
        }

        public ScanInfoBuilder WithPrecursor(
            double precursorMz,
            int charge = 0,
            double isolationWidth = 2.0,
            int parentScanNumber = 0)
        {
            _scanInfo.ParentIonMZ = precursorMz;
            _scanInfo.ParentIonMonoisotopicMZ = precursorMz; // Same for simulated data
            _scanInfo.IsolationWindowWidthMZ = isolationWidth;
            _scanInfo.ParentScan = parentScanNumber;

            // Add to scan events
            _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
                "Monoisotopic M/Z:", precursorMz.ToString("F6")));
            if (charge > 0)
            {
                _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
                    "Charge State:", charge.ToString()));
            }
            _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
                "MS2 Isolation Width:", isolationWidth.ToString("F2")));

            return this;
        }

        public ScanInfoBuilder WithFragmentation(
            ActivationType activationType,
            double collisionEnergy)
        {
            _scanInfo.ActivationType = activationType;
            _scanInfo.CollisionMode = activationType switch
            {
                ActivationType.HCD => "hcd",
                ActivationType.CID => "cid",
                ActivationType.ETD => "etd",
                ActivationType.ECD => "ecd",
                _ => "hcd"
            };

            // Add parent ion info
            var parentIon = new ParentIonInfo
            {
                MSLevel = _scanInfo.MSLevel,
                ParentIonMZ = _scanInfo.ParentIonMZ,
                CollisionMode = _scanInfo.CollisionMode,
                CollisionEnergy = (float)collisionEnergy,
                ActivationType = activationType
            };
            _scanInfo.ParentIons.Add(parentIon);

            return this;
        }

        public ScanInfoBuilder WithPolarity(IonMode polarity)
        {
            _scanInfo.IonMode = polarity;
            return this;
        }

        public ScanInfoBuilder AsCentroid(bool isCentroid = true)
        {
            _scanInfo.IsCentroided = isCentroid;
            return this;
        }

        public ScanInfoBuilder AsHighResolution(bool isHighRes = true)
        {
            _scanInfo.IsHighResolution = isHighRes;
            return this;
        }

        public ScanInfoBuilder WithStatistics(
            int numPeaks,
            double tic,
            double basePeakMz,
            double basePeakIntensity,
            double lowMass,
            double highMass)
        {
            _scanInfo.NumPeaks = numPeaks;
            _scanInfo.TotalIonCurrent = tic;
            _scanInfo.BasePeakMZ = basePeakMz;
            _scanInfo.BasePeakIntensity = basePeakIntensity;
            _scanInfo.LowMass = lowMass;
            _scanInfo.HighMass = highMass;
            return this;
        }

        public ScanInfoBuilder WithStatistics(ScanStatistics stats)
        {
            _scanInfo.NumPeaks = (int)stats.TIC; // Approximate
            _scanInfo.TotalIonCurrent = stats.TIC;
            _scanInfo.BasePeakMZ = stats.BasePeakMass;
            _scanInfo.BasePeakIntensity = stats.BasePeakIntensity;
            _scanInfo.LowMass = stats.LowMass;
            _scanInfo.HighMass = stats.HighMass;
            return this;
        }

        public ScanInfoBuilder WithFilterText(string filterText)
        {
            _scanInfo.FilterText = filterText;
            return this;
        }

        public ScanInfoBuilder WithGeneratedFilterText(
            double lowMass = 100,
            double highMass = 2000,
            double collisionEnergy = 30)
        {
            if (_scanInfo.MSLevel == 1)
            {
                _scanInfo.FilterText = FilterStringGenerator.GenerateMS1(
                    _scanInfo.IonMode,
                    _scanInfo.IsCentroided,
                    lowMass,
                    highMass);
            }
            else
            {
                _scanInfo.FilterText = FilterStringGenerator.GenerateMS2(
                    _scanInfo.ParentIonMZ,
                    _scanInfo.ActivationType,
                    collisionEnergy,
                    _scanInfo.IonMode,
                    _scanInfo.IsCentroided,
                    lowMass,
                    highMass);
            }
            return this;
        }

        public ScanInfoBuilder WithIonInjectionTime(double milliseconds)
        {
            _scanInfo.IonInjectionTime = milliseconds;
            _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
                "Ion Injection Time (ms):", milliseconds.ToString("F2")));
            return this;
        }

        public ScanInfoBuilder WithScanEvent(string name, string value)
        {
            _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(name, value));
            return this;
        }

        public ScanInfoBuilder AsDIA(bool isDIA = true)
        {
            _scanInfo.IsDIA = isDIA;
            return this;
        }

        public ScanInfoBuilder AsSIM(bool isSIM = true)
        {
            _scanInfo.SIMScan = isSIM;
            if (isSIM) _scanInfo.MRMScanType = MRMScanType.SIM;
            return this;
        }

        public ScanInfo Build()
        {
            // Set defaults
            if (_scanInfo.MSLevel == 0) _scanInfo.MSLevel = 1;
            if (_scanInfo.EventNumber == 0) _scanInfo.EventNumber = 1;

            // Auto-generate filter text if not set
            if (string.IsNullOrEmpty(_scanInfo.FilterText))
            {
                WithGeneratedFilterText();
            }

            return _scanInfo;
        }
    }
}
```

### 6.3 RawFileInfo Builder

```csharp
namespace VirtualOrbitrap.Builders
{
    /// <summary>
    /// Builder for RawFileInfo (file-level metadata).
    /// </summary>
    public class RawFileInfoBuilder
    {
        private readonly RawFileInfo _info = new();

        public RawFileInfoBuilder WithSampleName(string sampleName)
        {
            _info.SampleName = sampleName;
            return this;
        }

        public RawFileInfoBuilder WithInstrument(
            string name = "Virtual Orbitrap",
            string model = "Virtual Orbitrap Simulator",
            string serialNumber = "SIM-001")
        {
            _info.InstName = name;
            _info.InstModel = model;
            _info.InstSerialNumber = serialNumber;
            return this;
        }

        public RawFileInfoBuilder WithScanRange(
            int firstScan,
            int lastScan,
            double firstTimeMinutes,
            double lastTimeMinutes)
        {
            _info.ScanStart = firstScan;
            _info.ScanEnd = lastScan;
            _info.FirstScanTimeMinutes = firstTimeMinutes;
            _info.LastScanTimeMinutes = lastTimeMinutes;
            return this;
        }

        public RawFileInfoBuilder WithResolution(double resolution)
        {
            _info.MassResolution = resolution;
            return this;
        }

        public RawFileInfoBuilder WithCreationDate(DateTime date)
        {
            _info.CreationDate = date;
            return this;
        }

        public RawFileInfoBuilder WithComment(string comment)
        {
            _info.Comment1 = comment;
            return this;
        }

        public RawFileInfo Build()
        {
            if (_info.CreationDate == default)
                _info.CreationDate = DateTime.Now;
            return _info;
        }
    }
}
```

---

## 7. IAPI Event Emitter

### 7.1 IVirtualRawData Interface

```csharp
namespace VirtualOrbitrap.IAPI
{
    /// <summary>
    /// Interface matching key methods of Thermo's IRawDataPlus.
    /// Implement this to provide IAPI-compatible data access.
    /// </summary>
    public interface IVirtualRawData : IDisposable
    {
        //=================================================================
        // FILE METADATA
        //=================================================================

        /// <summary>
        /// File-level metadata.
        /// </summary>
        RawFileInfo FileInfo { get; }

        /// <summary>
        /// First scan number in the file.
        /// </summary>
        int ScanStart { get; }

        /// <summary>
        /// Last scan number in the file.
        /// </summary>
        int ScanEnd { get; }

        /// <summary>
        /// Total number of scans.
        /// </summary>
        int NumScans { get; }

        //=================================================================
        // SCAN ACCESS
        //=================================================================

        /// <summary>
        /// Get centroid stream for a scan.
        /// Primary method for accessing Orbitrap peak data.
        /// </summary>
        CentroidStream GetCentroidStream(int scanNumber);

        /// <summary>
        /// Get scan metadata.
        /// </summary>
        ScanInfo GetScanInfo(int scanNumber);

        /// <summary>
        /// Get scan statistics (lighter weight than full ScanInfo).
        /// </summary>
        ScanStatistics GetScanStatistics(int scanNumber);

        /// <summary>
        /// Get retention time for a scan.
        /// </summary>
        double GetRetentionTime(int scanNumber);

        /// <summary>
        /// Get MS level for a scan.
        /// </summary>
        int GetMSLevel(int scanNumber);

        /// <summary>
        /// Get filter text for a scan.
        /// </summary>
        string GetFilterText(int scanNumber);

        //=================================================================
        // ALTERNATIVE DATA ACCESS
        //=================================================================

        /// <summary>
        /// Get label data (alternative to CentroidStream for some uses).
        /// </summary>
        FTLabelInfo[] GetScanLabelData(int scanNumber);

        /// <summary>
        /// Get mass precision data.
        /// </summary>
        MassPrecisionInfo[] GetScanPrecisionData(int scanNumber);

        //=================================================================
        // EVENTS
        //=================================================================

        /// <summary>
        /// Event raised when a new scan arrives.
        /// Use for streaming/real-time simulation.
        /// </summary>
        event EventHandler<ScanArrivedEventArgs> ScanArrived;
    }

    /// <summary>
    /// Event args for scan arrival events.
    /// </summary>
    public class ScanArrivedEventArgs : EventArgs
    {
        public int ScanNumber { get; set; }
        public double RetentionTime { get; set; }
        public int MSLevel { get; set; }
        public CentroidStream CentroidStream { get; set; }
        public ScanInfo ScanInfo { get; set; }
    }
}
```

### 7.2 VirtualRawData Implementation

```csharp
namespace VirtualOrbitrap.IAPI
{
    /// <summary>
    /// Implementation of IVirtualRawData.
    /// Wraps loaded/simulated data and exposes via IAPI-compatible interface.
    /// </summary>
    public class VirtualRawData : IVirtualRawData
    {
        private readonly Dictionary<int, CentroidStream> _centroidStreams = new();
        private readonly Dictionary<int, ScanInfo> _scanInfos = new();
        private readonly Dictionary<int, ScanStatistics> _scanStats = new();

        public RawFileInfo FileInfo { get; private set; }
        public int ScanStart => FileInfo?.ScanStart ?? 1;
        public int ScanEnd => FileInfo?.ScanEnd ?? _centroidStreams.Count;
        public int NumScans => ScanEnd - ScanStart + 1;

        public event EventHandler<ScanArrivedEventArgs> ScanArrived;

        /// <summary>
        /// Create a new VirtualRawData instance.
        /// </summary>
        public VirtualRawData(RawFileInfo fileInfo = null)
        {
            FileInfo = fileInfo ?? new RawFileInfoBuilder().Build();
        }

        /// <summary>
        /// Add a scan to the virtual file.
        /// </summary>
        public void AddScan(
            int scanNumber,
            CentroidStream centroidStream,
            ScanInfo scanInfo,
            ScanStatistics stats = null)
        {
            _centroidStreams[scanNumber] = centroidStream;
            _scanInfos[scanNumber] = scanInfo;

            if (stats == null)
            {
                stats = ScanStatsCalculator.Calculate(centroidStream, scanInfo.RetentionTime);
            }
            _scanStats[scanNumber] = stats;

            // Update file info
            if (scanNumber > FileInfo.ScanEnd)
                FileInfo.ScanEnd = scanNumber;
            if (scanNumber < FileInfo.ScanStart || FileInfo.ScanStart == 0)
                FileInfo.ScanStart = scanNumber;
        }

        public CentroidStream GetCentroidStream(int scanNumber)
        {
            return _centroidStreams.TryGetValue(scanNumber, out var stream)
                ? stream
                : null;
        }

        public ScanInfo GetScanInfo(int scanNumber)
        {
            return _scanInfos.TryGetValue(scanNumber, out var info)
                ? info
                : null;
        }

        public ScanStatistics GetScanStatistics(int scanNumber)
        {
            return _scanStats.TryGetValue(scanNumber, out var stats)
                ? stats
                : null;
        }

        public double GetRetentionTime(int scanNumber)
        {
            return _scanInfos.TryGetValue(scanNumber, out var info)
                ? info.RetentionTime
                : 0;
        }

        public int GetMSLevel(int scanNumber)
        {
            return _scanInfos.TryGetValue(scanNumber, out var info)
                ? info.MSLevel
                : 0;
        }

        public string GetFilterText(int scanNumber)
        {
            return _scanInfos.TryGetValue(scanNumber, out var info)
                ? info.FilterText
                : string.Empty;
        }

        public FTLabelInfo[] GetScanLabelData(int scanNumber)
        {
            var stream = GetCentroidStream(scanNumber);
            if (stream == null) return Array.Empty<FTLabelInfo>();

            var labels = new FTLabelInfo[stream.Length];
            for (int i = 0; i < stream.Length; i++)
            {
                labels[i] = new FTLabelInfo
                {
                    Mass = stream.Masses[i],
                    Intensity = stream.Intensities[i],
                    Resolution = (float)(stream.Resolutions?[i] ?? 0),
                    Baseline = (float)(stream.Baselines?[i] ?? 0),
                    Noise = (float)(stream.Noises?[i] ?? 0),
                    Charge = stream.Charges?[i] ?? 0
                };
            }
            return labels;
        }

        public MassPrecisionInfo[] GetScanPrecisionData(int scanNumber)
        {
            var stream = GetCentroidStream(scanNumber);
            if (stream == null) return Array.Empty<MassPrecisionInfo>();

            var precision = new MassPrecisionInfo[stream.Length];
            for (int i = 0; i < stream.Length; i++)
            {
                precision[i] = new MassPrecisionInfo
                {
                    Mass = stream.Masses[i],
                    Intensity = stream.Intensities[i],
                    Resolution = stream.Resolutions?[i] ?? 0,
                    AccuracyPPM = 3.0, // Typical Orbitrap accuracy
                    AccuracyMMU = stream.Masses[i] * 3.0 / 1e6
                };
            }
            return precision;
        }

        /// <summary>
        /// Raise ScanArrived event (for streaming simulation).
        /// </summary>
        public void EmitScan(int scanNumber)
        {
            var stream = GetCentroidStream(scanNumber);
            var info = GetScanInfo(scanNumber);

            ScanArrived?.Invoke(this, new ScanArrivedEventArgs
            {
                ScanNumber = scanNumber,
                RetentionTime = info?.RetentionTime ?? 0,
                MSLevel = info?.MSLevel ?? 1,
                CentroidStream = stream,
                ScanInfo = info
            });
        }

        /// <summary>
        /// Emit all scans with configurable delay (real-time simulation).
        /// </summary>
        public async Task EmitAllScansAsync(
            TimeSpan delayBetweenScans,
            CancellationToken cancellationToken = default)
        {
            for (int scan = ScanStart; scan <= ScanEnd; scan++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                EmitScan(scan);
                await Task.Delay(delayBetweenScans, cancellationToken);
            }
        }

        public void Dispose()
        {
            _centroidStreams.Clear();
            _scanInfos.Clear();
            _scanStats.Clear();
        }
    }
}
```

---

## 8. Build Order & Dependencies

### 8.1 Project Structure

```
VirtualOrbitrap/
├── VirtualOrbitrap.sln
├── src/
│   ├── VirtualOrbitrap.Schema/           # Data models
│   │   ├── CentroidStream.cs
│   │   ├── ScanInfo.cs
│   │   ├── ScanStatistics.cs
│   │   ├── RawFileInfo.cs
│   │   ├── ParentIonInfo.cs
│   │   ├── MRMInfo.cs
│   │   ├── FTLabelInfo.cs
│   │   ├── MassPrecisionInfo.cs
│   │   └── Enums.cs
│   │
│   ├── VirtualOrbitrap.Enrichment/       # Data enhancement
│   │   ├── ResolutionCalculator.cs
│   │   ├── NoiseSynthesizer.cs
│   │   ├── BaselineGenerator.cs
│   │   ├── FilterStringGenerator.cs
│   │   └── ScanStatsCalculator.cs
│   │
│   ├── VirtualOrbitrap.Builders/         # Object factories
│   │   ├── CentroidStreamBuilder.cs
│   │   ├── ScanInfoBuilder.cs
│   │   └── RawFileInfoBuilder.cs
│   │
│   ├── VirtualOrbitrap.Parsers/          # Input parsing
│   │   ├── MzMLParser.cs
│   │   └── Ms2Parser.cs                  # Optional: direct ms2 parsing
│   │
│   └── VirtualOrbitrap.IAPI/             # Interface layer
│       ├── IVirtualRawData.cs
│       ├── VirtualRawData.cs
│       └── ScanArrivedEventArgs.cs
│
├── tests/
│   ├── VirtualOrbitrap.Schema.Tests/
│   ├── VirtualOrbitrap.Enrichment.Tests/
│   ├── VirtualOrbitrap.Builders.Tests/
│   └── VirtualOrbitrap.IAPI.Tests/
│
└── samples/
    ├── BasicUsage/
    └── StreamingSimulation/
```

### 8.2 NuGet Dependencies

```xml
<ItemGroup>
  <!-- XML parsing for mzML -->
  <PackageReference Include="System.Xml.Linq" Version="4.3.0" />

  <!-- Optional: High-performance parsing -->
  <PackageReference Include="System.IO.Pipelines" Version="8.0.0" />

  <!-- Testing -->
  <PackageReference Include="xunit" Version="2.6.0" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
</ItemGroup>
```

### 8.3 Build Order (Phase-by-Phase)

```
Phase 1: Schema (No dependencies)
├── Enums.cs
├── CentroidStream.cs
├── ScanInfo.cs
├── ScanStatistics.cs
├── RawFileInfo.cs
├── ParentIonInfo.cs
└── Other structs

Phase 2: Enrichment (Depends on: Schema)
├── ResolutionCalculator.cs
├── NoiseSynthesizer.cs
├── BaselineGenerator.cs
├── FilterStringGenerator.cs
└── ScanStatsCalculator.cs

Phase 3: Builders (Depends on: Schema, Enrichment)
├── CentroidStreamBuilder.cs
├── ScanInfoBuilder.cs
└── RawFileInfoBuilder.cs

Phase 4: Parsers (Depends on: Schema)
├── MzMLParser.cs
└── Ms2Parser.cs

Phase 5: IAPI (Depends on: All above)
├── IVirtualRawData.cs
└── VirtualRawData.cs
```

### 8.4 Development Milestones

| Milestone | Components | Estimated LOC | Deliverable |
|-----------|------------|---------------|-------------|
| M1: Schema | All schema classes | 400 | Compilable schema library |
| M2: Enrichment | All enrichment classes | 200 | Enrichment functions |
| M3: Builders | All builder classes | 250 | Object factory layer |
| M4: Parser | mzML parser | 150 | Can load mzML files |
| M5: IAPI | Interface + implementation | 200 | Full IAPI emitter |
| **TOTAL** | | **~1200 LOC** | MVP Complete |

---

## 9. Testing Strategy

### 9.1 Unit Test Examples

```csharp
namespace VirtualOrbitrap.Tests
{
    public class ResolutionCalculatorTests
    {
        [Fact]
        public void Calculate_AtReferenceMass_ReturnsR0()
        {
            // Arrange
            double[] masses = { 200.0 };
            double r0 = 120000;
            double m0 = 200;

            // Act
            var resolutions = ResolutionCalculator.Calculate(masses, r0, m0);

            // Assert
            resolutions[0].Should().BeApproximately(r0, 1);
        }

        [Fact]
        public void Calculate_AtHigherMass_ReturnsLowerResolution()
        {
            // Arrange
            double[] masses = { 200.0, 800.0 };
            double r0 = 120000;

            // Act
            var resolutions = ResolutionCalculator.Calculate(masses, r0);

            // Assert
            resolutions[1].Should().BeLessThan(resolutions[0]);
            // At 800 m/z, resolution should be 120000 * sqrt(200/800) = 60000
            resolutions[1].Should().BeApproximately(60000, 100);
        }
    }

    public class CentroidStreamBuilderTests
    {
        [Fact]
        public void Build_WithPeaks_CreateValidStream()
        {
            // Arrange
            var masses = new double[] { 500.0, 600.0, 700.0 };
            var intensities = new double[] { 1000.0, 5000.0, 2000.0 };

            // Act
            var stream = new CentroidStreamBuilder()
                .WithScanNumber(1)
                .WithPeaks(masses, intensities)
                .Build();

            // Assert
            stream.Validate().Should().BeTrue();
            stream.Length.Should().Be(3);
            stream.Resolutions.Should().NotBeNull();
            stream.Noises.Should().NotBeNull();
            stream.Baselines.Should().NotBeNull();
            stream.BasePeakMass.Should().Be(600.0);
            stream.BasePeakIntensity.Should().Be(5000.0);
        }
    }

    public class FilterStringGeneratorTests
    {
        [Fact]
        public void GenerateMS1_ReturnsValidFilterString()
        {
            // Act
            var filter = FilterStringGenerator.GenerateMS1(
                IonMode.Positive, true, 100, 2000);

            // Assert
            filter.Should().StartWith("FTMS");
            filter.Should().Contain("+");
            filter.Should().Contain("c");
            filter.Should().Contain("Full ms");
            filter.Should().Contain("[100.00-2000.00]");
        }

        [Fact]
        public void GenerateMS2_IncludesPrecursorAndActivation()
        {
            // Act
            var filter = FilterStringGenerator.GenerateMS2(
                precursorMz: 750.5,
                activationType: ActivationType.HCD,
                collisionEnergy: 35);

            // Assert
            filter.Should().Contain("ms2");
            filter.Should().Contain("750.50@hcd35.00");
            filter.Should().Contain("d"); // data dependent
        }
    }
}
```

### 9.2 Integration Test: Full Pipeline

```csharp
public class FullPipelineTests
{
    [Fact]
    public async Task EndToEnd_LoadMzML_EmitScans()
    {
        // Arrange
        var mzmlPath = "test_data/sample.mzML";
        var parser = new MzMLParser();
        var virtualRaw = new VirtualRawData();

        // Act: Load mzML
        var scans = parser.Parse(mzmlPath);

        // Build and add scans
        foreach (var scanData in scans)
        {
            var stream = new CentroidStreamBuilder()
                .WithScanNumber(scanData.ScanNumber)
                .WithPeaks(scanData.Masses, scanData.Intensities)
                .WithCalculatedResolutions(120000)
                .WithSynthesizedNoise()
                .WithSynthesizedBaseline()
                .Build();

            var info = new ScanInfoBuilder()
                .WithScanNumber(scanData.ScanNumber)
                .WithMSLevel(scanData.MSLevel)
                .WithRetentionTime(scanData.RetentionTime)
                .WithPolarity(IonMode.Positive)
                .AsCentroid()
                .AsHighResolution()
                .WithGeneratedFilterText()
                .Build();

            virtualRaw.AddScan(scanData.ScanNumber, stream, info);
        }

        // Assert
        virtualRaw.NumScans.Should().BeGreaterThan(0);

        var firstStream = virtualRaw.GetCentroidStream(1);
        firstStream.Should().NotBeNull();
        firstStream.Validate().Should().BeTrue();

        var firstInfo = virtualRaw.GetScanInfo(1);
        firstInfo.Should().NotBeNull();
        firstInfo.FilterText.Should().NotBeNullOrEmpty();
    }
}
```

---

## 10. Future Enhancements (v1.1+)

### 10.1 Isotope Envelope Generation (Deferred)

```csharp
// v1.1 Implementation
namespace VirtualOrbitrap.Enrichment.v11
{
    public static class IsotopeEnvelopeGenerator
    {
        // Averagine formula: C4.9384 H7.7583 N1.3577 O1.4773 S0.0417
        private const double AveragineC = 4.9384;
        private const double AveragineH = 7.7583;
        private const double AveragineN = 1.3577;
        private const double AveragineO = 1.4773;
        private const double AveragineS = 0.0417;
        private const double AveragineMass = 111.1254; // Average residue mass

        public static List<(double mz, double intensity)> Generate(
            double monoMz,
            int charge,
            double monoIntensity,
            int maxIsotopes = 5)
        {
            var envelope = new List<(double, double)>();
            double spacing = 1.00335 / charge;

            // Estimate number of residues
            double mass = monoMz * charge;
            int residues = (int)(mass / AveragineMass);

            // Calculate isotope intensities using Poisson approximation
            double lambda = residues * 0.0111; // ~1.1% 13C natural abundance

            for (int i = 0; i < maxIsotopes; i++)
            {
                double relIntensity = PoissonProbability(lambda, i);
                envelope.Add((monoMz + i * spacing, monoIntensity * relIntensity));
            }

            return envelope;
        }

        private static double PoissonProbability(double lambda, int k)
        {
            return Math.Pow(lambda, k) * Math.Exp(-lambda) / Factorial(k);
        }

        private static double Factorial(int n)
        {
            if (n <= 1) return 1;
            double result = 1;
            for (int i = 2; i <= n; i++) result *= i;
            return result;
        }
    }
}
```

### 10.2 Per-Peak Charge Estimation (Deferred)

```csharp
// v1.1 Implementation
namespace VirtualOrbitrap.Enrichment.v11
{
    public static class ChargeEstimator
    {
        private const double IsotopeSpacing = 1.00335; // 13C - 12C mass difference

        public static int[] EstimateCharges(double[] masses, double[] intensities, double tolerance = 0.01)
        {
            var charges = new int[masses.Length];

            for (int i = 0; i < masses.Length; i++)
            {
                charges[i] = EstimateSingleCharge(masses, intensities, i, tolerance);
            }

            return charges;
        }

        private static int EstimateSingleCharge(
            double[] masses, double[] intensities, int index, double tolerance)
        {
            double targetMass = masses[index];

            // Try charges 1-6
            for (int z = 1; z <= 6; z++)
            {
                double expectedSpacing = IsotopeSpacing / z;
                double nextIsotopeMass = targetMass + expectedSpacing;

                // Look for next isotope peak
                for (int j = index + 1; j < masses.Length && masses[j] < nextIsotopeMass + 0.5; j++)
                {
                    if (Math.Abs(masses[j] - nextIsotopeMass) < tolerance)
                    {
                        // Found potential isotope - check intensity ratio
                        double ratio = intensities[j] / intensities[index];
                        if (ratio > 0.1 && ratio < 2.0) // Reasonable isotope ratio
                        {
                            return z;
                        }
                    }
                }
            }

            return 0; // Undetermined
        }
    }
}
```

### 10.3 Roadmap

| Version | Features | Target |
|---------|----------|--------|
| v1.0 | Core pipeline, enrichment, IAPI interface | MVP |
| v1.1 | Isotope envelopes, charge estimation | Post-MVP |
| v1.2 | Mass accuracy modeling, DIA support | Future |
| v2.0 | Real-time streaming, multi-threading | Future |

---

## Appendix A: Example Usage

### A.1 Basic Usage

```csharp
using VirtualOrbitrap.Schema;
using VirtualOrbitrap.Builders;
using VirtualOrbitrap.IAPI;

// Create a virtual raw file
var virtualRaw = new VirtualRawData(
    new RawFileInfoBuilder()
        .WithSampleName("Test Sample")
        .WithInstrument("Virtual Orbitrap", "Exploris 480 Simulator", "SIM-001")
        .WithResolution(120000)
        .Build()
);

// Add an MS1 scan
var ms1Stream = new CentroidStreamBuilder()
    .WithScanNumber(1)
    .WithPeaks(
        masses: new double[] { 500.2, 600.3, 700.4, 800.5 },
        intensities: new double[] { 10000, 50000, 25000, 15000 })
    .WithCalculatedResolutions(120000)
    .WithSynthesizedNoise()
    .WithSynthesizedBaseline()
    .Build();

var ms1Info = new ScanInfoBuilder()
    .WithScanNumber(1)
    .WithMSLevel(1)
    .WithRetentionTime(1.5)
    .WithPolarity(IonMode.Positive)
    .AsCentroid()
    .AsHighResolution()
    .WithGeneratedFilterText()
    .Build();

virtualRaw.AddScan(1, ms1Stream, ms1Info);

// Add an MS2 scan
var ms2Stream = new CentroidStreamBuilder()
    .WithScanNumber(2)
    .WithPeaks(
        masses: new double[] { 250.1, 350.2, 450.3, 550.4 },
        intensities: new double[] { 5000, 20000, 15000, 8000 })
    .Build();

var ms2Info = new ScanInfoBuilder()
    .WithScanNumber(2)
    .WithMSLevel(2)
    .WithRetentionTime(1.52)
    .WithPrecursor(600.3, charge: 2, isolationWidth: 2.0, parentScanNumber: 1)
    .WithFragmentation(ActivationType.HCD, collisionEnergy: 30)
    .WithPolarity(IonMode.Positive)
    .AsCentroid()
    .AsHighResolution()
    .WithGeneratedFilterText()
    .Build();

virtualRaw.AddScan(2, ms2Stream, ms2Info);

// Access data via IAPI-compatible interface
var stream = virtualRaw.GetCentroidStream(1);
Console.WriteLine($"Scan 1 has {stream.Length} peaks");
Console.WriteLine($"Base peak: {stream.BasePeakMass:F4} m/z, {stream.BasePeakIntensity:F0} intensity");

var info = virtualRaw.GetScanInfo(2);
Console.WriteLine($"Scan 2 filter: {info.FilterText}");
Console.WriteLine($"Precursor: {info.ParentIonMZ:F4} m/z");
```

### A.2 Streaming Simulation

```csharp
// Subscribe to scan events
virtualRaw.ScanArrived += (sender, e) =>
{
    Console.WriteLine($"Scan {e.ScanNumber} arrived at RT {e.RetentionTime:F2} min");
    Console.WriteLine($"  MS{e.MSLevel}, {e.CentroidStream.Length} peaks");
};

// Emit scans with 100ms delay between each
await virtualRaw.EmitAllScansAsync(
    delayBetweenScans: TimeSpan.FromMilliseconds(100));
```

---

## Appendix B: Filter String Reference

### B.1 Filter String Components

```
FTMS + p NSI d Full ms2 750.50@hcd35.00 [200.00-2000.00]
│     │ │ │   │    │    │      │        │
│     │ │ │   │    │    │      │        └── Mass range
│     │ │ │   │    │    │      └── Collision energy
│     │ │ │   │    │    └── Activation type
│     │ │ │   │    └── Precursor m/z
│     │ │ │   └── Scan mode (Full, SIM, SRM)
│     │ │ └── Data dependent flag
│     │ └── Ionization (NSI, ESI, APCI)
│     └── Profile/Centroid (p/c)
└── Mass analyzer (FTMS, ITMS, ASTMS)
     └── Polarity (+/-)
```

### B.2 Common Filter Patterns

| Scan Type | Filter String Example |
|-----------|----------------------|
| MS1 Profile | `FTMS + p NSI Full ms [100.00-2000.00]` |
| MS1 Centroid | `FTMS + c NSI Full ms [100.00-2000.00]` |
| MS2 HCD | `FTMS + p NSI d Full ms2 750.50@hcd35.00 [200.00-2000.00]` |
| MS2 CID | `FTMS + p NSI d Full ms2 750.50@cid35.00 [200.00-2000.00]` |
| MS3 | `FTMS + p NSI d Full ms3 750.50@hcd35.00 450.25@hcd35.00 [100.00-2000.00]` |
| MS2 Negative | `FTMS - p NSI d Full ms2 750.50@hcd35.00 [200.00-2000.00]` |
| SIM | `FTMS + c NSI SIM ms [499.50-500.50]` |

---

*Document Version: 1.0*
*Last Updated: December 2024*
*Target: Virtual Orbitrap IAPI Simulator MVP*
