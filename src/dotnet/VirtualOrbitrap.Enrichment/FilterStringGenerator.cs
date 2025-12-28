using System.Text;
using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.Enrichment;

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
