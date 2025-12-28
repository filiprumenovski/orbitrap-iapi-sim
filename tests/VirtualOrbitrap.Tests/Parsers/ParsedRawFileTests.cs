using FluentAssertions;
using VirtualOrbitrap.Parsers.Dto;
using Xunit;

namespace VirtualOrbitrap.Tests.Parsers;

public class ParsedRawFileTests
{
    [Fact]
    public void ParsedRawFile_Empty_ShouldHaveZeroScans()
    {
        // Arrange & Act
        var file = new ParsedRawFile();

        // Assert
        file.TotalScans.Should().Be(0);
        file.FirstScanNumber.Should().Be(0);
        file.LastScanNumber.Should().Be(0);
        file.StartTime.Should().Be(0);
        file.EndTime.Should().Be(0);
    }

    [Fact]
    public void ParsedRawFile_WithScans_ShouldReportCorrectMetadata()
    {
        // Arrange
        var scans = new List<ParsedScan>
        {
            new() { ScanNumber = 1, MsLevel = 1, RetentionTimeMinutes = 0.5 },
            new() { ScanNumber = 2, MsLevel = 2, RetentionTimeMinutes = 0.6 },
            new() { ScanNumber = 3, MsLevel = 1, RetentionTimeMinutes = 1.0 },
            new() { ScanNumber = 4, MsLevel = 2, RetentionTimeMinutes = 1.1 },
            new() { ScanNumber = 5, MsLevel = 2, RetentionTimeMinutes = 1.2 }
        };

        // Act
        var file = new ParsedRawFile { Scans = scans };

        // Assert
        file.TotalScans.Should().Be(5);
        file.FirstScanNumber.Should().Be(1);
        file.LastScanNumber.Should().Be(5);
        file.StartTime.Should().Be(0.5);
        file.EndTime.Should().Be(1.2);
    }

    [Fact]
    public void ParsedRawFile_Ms1Scans_ShouldFilterCorrectly()
    {
        // Arrange
        var scans = new List<ParsedScan>
        {
            new() { ScanNumber = 1, MsLevel = 1 },
            new() { ScanNumber = 2, MsLevel = 2 },
            new() { ScanNumber = 3, MsLevel = 1 },
            new() { ScanNumber = 4, MsLevel = 2 },
            new() { ScanNumber = 5, MsLevel = 2 }
        };
        var file = new ParsedRawFile { Scans = scans };

        // Act
        var ms1Scans = file.Ms1Scans.ToList();

        // Assert
        ms1Scans.Should().HaveCount(2);
        ms1Scans.Select(s => s.ScanNumber).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void ParsedRawFile_MsnScans_ShouldFilterCorrectly()
    {
        // Arrange
        var scans = new List<ParsedScan>
        {
            new() { ScanNumber = 1, MsLevel = 1 },
            new() { ScanNumber = 2, MsLevel = 2 },
            new() { ScanNumber = 3, MsLevel = 1 },
            new() { ScanNumber = 4, MsLevel = 2 },
            new() { ScanNumber = 5, MsLevel = 3 }
        };
        var file = new ParsedRawFile { Scans = scans };

        // Act
        var msnScans = file.MsnScans.ToList();

        // Assert
        msnScans.Should().HaveCount(3);
        msnScans.Select(s => s.ScanNumber).Should().BeEquivalentTo(new[] { 2, 4, 5 });
    }
}
