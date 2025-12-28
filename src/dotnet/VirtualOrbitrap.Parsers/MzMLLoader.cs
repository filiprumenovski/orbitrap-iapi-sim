using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using VirtualOrbitrap.Parsers.Dto;

namespace VirtualOrbitrap.Parsers;

/// <summary>
/// Minimal mzML parser implemented using XML (cross-platform).
/// Supports centroid spectra with uncompressed base64-encoded m/z and intensity arrays.
/// </summary>
public sealed class MzMLLoader : IMzMLLoader
{
    private static readonly XNamespace MzmlNs = "http://psi.hupo.org/ms/mzml";

    public async Task<ParsedRawFile> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"mzML file not found: {filePath}", filePath);

        return await Task.Run(() => LoadSync(filePath), cancellationToken);
    }

    public async IAsyncEnumerable<ParsedScan> StreamScansAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"mzML file not found: {filePath}", filePath);

        var parsed = await Task.Run(() => LoadSync(filePath), cancellationToken);
        foreach (var scan in parsed.Scans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return scan;
        }
    }

    private static ParsedRawFile LoadSync(string filePath)
    {
        var doc = XDocument.Load(filePath, LoadOptions.None);

        var spectra = doc
            .Descendants(MzmlNs + "spectrum")
            .Select(ParseSpectrum)
            .OrderBy(s => s.ScanNumber)
            .ToList();

        var creation = TryParseMzmlCreationDate(doc) ?? File.GetCreationTime(filePath);

        return new ParsedRawFile
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            CreationDate = creation,
            InstrumentModel = ExtractInstrumentModel(doc),
            Scans = spectra
        };
    }

    private static ParsedScan ParseSpectrum(XElement spectrum)
    {
        var id = (string?)spectrum.Attribute("id") ?? string.Empty;
        var index = TryParseInt((string?)spectrum.Attribute("index")) ?? 0;
        var scanNumber = ParseScanNumber(id) ?? (index + 1);

        var msLevel = TryParseInt(GetCvParamValue(spectrum, "MS:1000511")) ?? 1;
        var isCentroid = HasCvParam(spectrum, "MS:1000127");
        var polarity = HasCvParam(spectrum, "MS:1000130") ? 1 : HasCvParam(spectrum, "MS:1000129") ? -1 : 0;

        var rtMinutes = ParseRetentionTimeMinutes(spectrum) ?? 0.0;

        var (mzs, intensities) = ParseMzIntensityArrays(spectrum);

        // TIC + base peak
        double tic = 0;
        double basePeakMz = 0;
        double basePeakIntensity = 0;
        for (int i = 0; i < intensities.Length; i++)
        {
            var intensity = intensities[i];
            tic += intensity;
            if (intensity > basePeakIntensity)
            {
                basePeakIntensity = intensity;
                basePeakMz = mzs.Length > i ? mzs[i] : 0;
            }
        }

        var lowMz = mzs.Length > 0 ? mzs[0] : 0;
        var highMz = mzs.Length > 0 ? mzs[^1] : 0;

        PrecursorInfo? precursor = null;
        if (msLevel > 1)
        {
            precursor = ParsePrecursor(spectrum);
        }

        return new ParsedScan
        {
            Index = index,
            ScanNumber = scanNumber,
            MsLevel = msLevel,
            RetentionTimeMinutes = rtMinutes,
            Mzs = mzs,
            Intensities = intensities,
            IsCentroid = isCentroid,
            Polarity = polarity,
            TotalIonCurrent = tic,
            BasePeakMz = basePeakMz,
            BasePeakIntensity = basePeakIntensity,
            LowMz = lowMz,
            HighMz = highMz,
            Precursor = precursor,
            InjectionTimeMs = null
        };
    }

    private static (double[] Mzs, double[] Intensities) ParseMzIntensityArrays(XElement spectrum)
    {
        double[] mzs = Array.Empty<double>();
        double[] intensities = Array.Empty<double>();

        foreach (var bda in spectrum.Descendants(MzmlNs + "binaryDataArray"))
        {
            var isMz = HasCvParam(bda, "MS:1000514");
            var isIntensity = HasCvParam(bda, "MS:1000515");
            if (!isMz && !isIntensity)
                continue;

            var is64 = HasCvParam(bda, "MS:1000523");
            var is32 = HasCvParam(bda, "MS:1000521");
            if (!is64 && !is32)
                is64 = true;

            if (!HasCvParam(bda, "MS:1000576"))
            {
                if (HasCvParam(bda, "MS:1000574"))
                    throw new NotSupportedException("zlib-compressed mzML binary arrays are not supported by the minimal XML loader.");
            }

            var binaryText = (string?)bda.Element(MzmlNs + "binary") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(binaryText))
                continue;

            var bytes = Convert.FromBase64String(binaryText.Trim());
            var values = is32 ? DecodeFloat32(bytes) : DecodeFloat64(bytes);

            if (isMz)
                mzs = values;
            else if (isIntensity)
                intensities = values;
        }

        if (mzs.Length != intensities.Length && mzs.Length > 0 && intensities.Length > 0)
            throw new FormatException($"mzML spectrum arrays have different lengths: mz={mzs.Length}, intensity={intensities.Length}");

        return (mzs, intensities);
    }

    private static double[] DecodeFloat64(byte[] bytes)
    {
        if (bytes.Length % 8 != 0)
            throw new FormatException($"Invalid float64 byte length: {bytes.Length}");

        var count = bytes.Length / 8;
        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToDouble(bytes, i * 8);
        }
        return values;
    }

    private static double[] DecodeFloat32(byte[] bytes)
    {
        if (bytes.Length % 4 != 0)
            throw new FormatException($"Invalid float32 byte length: {bytes.Length}");

        var count = bytes.Length / 4;
        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToSingle(bytes, i * 4);
        }
        return values;
    }

    private static double? ParseRetentionTimeMinutes(XElement spectrum)
    {
        var scan = spectrum.Descendants(MzmlNs + "scan").FirstOrDefault();
        if (scan == null)
            return null;

        var rtParam = scan.Elements(MzmlNs + "cvParam")
            .FirstOrDefault(e => (string?)e.Attribute("accession") == "MS:1000016");
        if (rtParam == null)
            return null;

        var value = ParseDoubleInvariant((string?)rtParam.Attribute("value"));
        if (!value.HasValue)
            return null;

        var unitAccession = (string?)rtParam.Attribute("unitAccession");
        return unitAccession switch
        {
            "UO:0000010" => value.Value / 60.0, // seconds
            _ => value.Value // minutes (default)
        };
    }

    private static PrecursorInfo? ParsePrecursor(XElement spectrum)
    {
        var precursor = spectrum.Descendants(MzmlNs + "precursor").FirstOrDefault();
        if (precursor == null)
            return null;

        var spectrumRef = (string?)precursor.Attribute("spectrumRef");
        var precursorScan = ParseScanNumber(spectrumRef ?? string.Empty) ?? 0;

        var isolation = precursor.Element(MzmlNs + "isolationWindow");
        var isoTarget = ParseDoubleInvariant(GetCvParamValue(isolation, "MS:1000827"));
        var isoLower = ParseDoubleInvariant(GetCvParamValue(isolation, "MS:1000828"));
        var isoUpper = ParseDoubleInvariant(GetCvParamValue(isolation, "MS:1000829"));

        var selectedIon = precursor.Descendants(MzmlNs + "selectedIon").FirstOrDefault();
        var selectedMz = ParseDoubleInvariant(GetCvParamValue(selectedIon, "MS:1000744"));
        if (!selectedMz.HasValue)
            return null;

        var charge = TryParseInt(GetCvParamValue(selectedIon, "MS:1000041")) ?? 0;
        var intensity = ParseDoubleInvariant(GetCvParamValue(selectedIon, "MS:1000042"));

        var activation = precursor.Element(MzmlNs + "activation");
        var collisionEnergy = ParseDoubleInvariant(GetCvParamValue(activation, "MS:1000045")) ?? 0.0;

        var activationMethod = "Unknown";
        if (HasCvParam(activation, "MS:1000422")) activationMethod = "HCD";
        else if (HasCvParam(activation, "MS:1000133")) activationMethod = "CID";
        else if (HasCvParam(activation, "MS:1000598")) activationMethod = "ETD";

        var target = isoTarget ?? selectedMz.Value;

        return new PrecursorInfo
        {
            SelectedMz = selectedMz.Value,
            MonoisotopicMz = null,
            IsolationWindowTargetMz = target,
            IsolationWindowLowerOffset = isoLower ?? 0.0,
            IsolationWindowUpperOffset = isoUpper ?? 0.0,
            Charge = charge,
            ActivationMethod = activationMethod,
            CollisionEnergy = collisionEnergy,
            Intensity = intensity,
            PrecursorScanNumber = precursorScan
        };
    }

    private static string ExtractInstrumentModel(XDocument doc)
    {
        var model = doc.Descendants(MzmlNs + "analyzer").Elements(MzmlNs + "cvParam")
            .Select(e => (string?)e.Attribute("name"))
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return model ?? "Unknown";
    }

    private static DateTime? TryParseMzmlCreationDate(XDocument doc)
    {
        // mzML doesn't have a single mandated creation timestamp field; leave null.
        _ = doc;
        return null;
    }

    private static bool HasCvParam(XElement? element, string accession)
        => element != null && element.Elements(MzmlNs + "cvParam").Any(e => (string?)e.Attribute("accession") == accession)
            || element != null && element.Descendants(MzmlNs + "cvParam").Any(e => (string?)e.Attribute("accession") == accession);

    private static string? GetCvParamValue(XElement? element, string accession)
    {
        if (element == null)
            return null;

        var cv = element.Elements(MzmlNs + "cvParam").FirstOrDefault(e => (string?)e.Attribute("accession") == accession)
                 ?? element.Descendants(MzmlNs + "cvParam").FirstOrDefault(e => (string?)e.Attribute("accession") == accession);
        return (string?)cv?.Attribute("value");
    }

    private static int? ParseScanNumber(string idOrRef)
    {
        if (string.IsNullOrWhiteSpace(idOrRef))
            return null;

        // Common mzML IDs: "scan=123" or "controllerType=0 controllerNumber=1 scan=123"
        var idx = idOrRef.LastIndexOf("scan=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + 5;
            var end = start;
            while (end < idOrRef.Length && char.IsDigit(idOrRef[end])) end++;
            return TryParseInt(idOrRef[start..end]);
        }

        // Fallback: take trailing digits
        var digitsStart = idOrRef.Length;
        while (digitsStart > 0 && char.IsDigit(idOrRef[digitsStart - 1])) digitsStart--;
        if (digitsStart < idOrRef.Length)
            return TryParseInt(idOrRef[digitsStart..]);

        return null;
    }

    private static int? TryParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    private static double? ParseDoubleInvariant(string? value)
        => double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) ? d : null;
}
