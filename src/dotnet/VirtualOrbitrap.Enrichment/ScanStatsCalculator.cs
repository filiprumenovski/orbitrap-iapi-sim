using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.Enrichment;

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
