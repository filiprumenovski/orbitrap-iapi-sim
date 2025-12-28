using FluentAssertions;
using VirtualOrbitrap.Pipeline;
using Xunit;

namespace VirtualOrbitrap.Tests.Pipeline;

public class PipelineOptionsTests
{
    [Fact]
    public void PipelineOptions_Defaults_ShouldHaveReasonableValues()
    {
        // Arrange & Act
        var options = new PipelineOptions();

        // Assert
        options.ResolutionR0.Should().Be(60000);
        options.ResolutionM0.Should().Be(200);
        options.ShotNoiseFactor.Should().Be(0.02);
        options.ElectronicNoiseFloor.Should().Be(100);
        options.IonizationMode.Should().Be("NSI");
        options.GenerateFilterStrings.Should().BeTrue();
        options.SynthesizeNoise.Should().BeTrue();
        options.SynthesizeBaseline.Should().BeTrue();
        options.CalculateResolutions.Should().BeTrue();
        options.ReplayMode.Should().Be(ReplayMode.Immediate);
        options.ReplayDelayMultiplier.Should().Be(1.0);
        options.FixedDelayMs.Should().Be(100);
    }

    [Fact]
    public void PipelineOptions_CustomSettings_ShouldBeApplied()
    {
        // Arrange & Act
        var options = new PipelineOptions
        {
            ResolutionR0 = 120000,
            RandomSeed = 42,
            ReplayMode = ReplayMode.RealTime,
            ReplayDelayMultiplier = 0.5,
            GenerateFilterStrings = false
        };

        // Assert
        options.ResolutionR0.Should().Be(120000);
        options.RandomSeed.Should().Be(42);
        options.ReplayMode.Should().Be(ReplayMode.RealTime);
        options.ReplayDelayMultiplier.Should().Be(0.5);
        options.GenerateFilterStrings.Should().BeFalse();
    }
}
