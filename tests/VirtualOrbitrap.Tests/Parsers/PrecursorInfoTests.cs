using FluentAssertions;
using VirtualOrbitrap.Parsers.Dto;
using Xunit;

namespace VirtualOrbitrap.Tests.Parsers;

public class PrecursorInfoTests
{
    [Fact]
    public void PrecursorInfo_IsolationWindowWidth_ShouldBeSumOfOffsets()
    {
        // Arrange & Act
        var precursor = new PrecursorInfo
        {
            IsolationWindowLowerOffset = 1.0,
            IsolationWindowUpperOffset = 1.0
        };

        // Assert
        precursor.IsolationWindowWidth.Should().Be(2.0);
    }

    [Fact]
    public void PrecursorInfo_AsymmetricWindow_ShouldCalculateCorrectWidth()
    {
        // Arrange & Act
        var precursor = new PrecursorInfo
        {
            IsolationWindowLowerOffset = 0.5,
            IsolationWindowUpperOffset = 1.5
        };

        // Assert
        precursor.IsolationWindowWidth.Should().Be(2.0);
    }

    [Fact]
    public void PrecursorInfo_DefaultValues_ShouldBeZeroOrEmpty()
    {
        // Arrange & Act
        var precursor = new PrecursorInfo();

        // Assert
        precursor.SelectedMz.Should().Be(0);
        precursor.MonoisotopicMz.Should().BeNull();
        precursor.Charge.Should().Be(0);
        precursor.ActivationMethod.Should().BeEmpty();
        precursor.CollisionEnergy.Should().Be(0);
        precursor.Intensity.Should().BeNull();
        precursor.PrecursorScanNumber.Should().Be(0);
    }
}
