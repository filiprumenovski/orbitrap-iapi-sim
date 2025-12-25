using Xunit;
using FluentAssertions;
using Orbitrap.Abstractions;

namespace Orbitrap.Abstractions.Tests;

public class ScanResultTests
{
    [Fact]
    public void Success_CreatesScanResultWithScan()
    {
        // Arrange
        var scan = CreateTestScan();

        // Act
        var result = ScanResult.Success(scan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Scan.Should().BeSameAs(scan);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithNullScan_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ScanResult.Success(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Failure_WithScanError_CreatesScanResultWithError()
    {
        // Arrange
        var error = new ScanError("TEST_ERROR", "Test error message");

        // Act
        var result = ScanResult.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Scan.Should().BeNull();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Failure_WithNullError_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ScanResult.Failure((ScanError)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Failure_WithCodeAndMessage_CreatesError()
    {
        // Act
        var result = ScanResult.Failure("ERR_CODE", "Error message");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ERR_CODE");
        result.Error.Message.Should().Be("Error message");
        result.Error.InnerException.Should().BeNull();
    }

    [Fact]
    public void Failure_WithException_IncludesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var result = ScanResult.Failure("ERR_CODE", "Error message", innerException);

        // Assert
        result.Error!.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Match_OnSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var scan = CreateTestScan();
        var result = ScanResult.Success(scan);
        IOrbitrapScan? capturedScan = null;
        ScanError? capturedError = null;

        // Act
        result.Match(
            onSuccess: s => capturedScan = s,
            onFailure: e => capturedError = e);

        // Assert
        capturedScan.Should().BeSameAs(scan);
        capturedError.Should().BeNull();
    }

    [Fact]
    public void Match_OnFailure_ExecutesFailureAction()
    {
        // Arrange
        var error = new ScanError("TEST", "test");
        var result = ScanResult.Failure(error);
        IOrbitrapScan? capturedScan = null;
        ScanError? capturedError = null;

        // Act
        result.Match(
            onSuccess: s => capturedScan = s,
            onFailure: e => capturedError = e);

        // Assert
        capturedScan.Should().BeNull();
        capturedError.Should().BeSameAs(error);
    }

    [Fact]
    public void Match_WithReturnValue_OnSuccess_ReturnsSuccessValue()
    {
        // Arrange
        var scan = CreateTestScan();
        var result = ScanResult.Success(scan);

        // Act
        var value = result.Match(
            onSuccess: s => s.ScanNumber,
            onFailure: e => -1);

        // Assert
        value.Should().Be(scan.ScanNumber);
    }

    [Fact]
    public void Match_WithReturnValue_OnFailure_ReturnsFailureValue()
    {
        // Arrange
        var result = ScanResult.Failure("ERR", "error");

        // Act
        var value = result.Match(
            onSuccess: s => s.ScanNumber,
            onFailure: e => -1);

        // Assert
        value.Should().Be(-1);
    }

    [Fact]
    public void GetScanOrThrow_OnSuccess_ReturnsScan()
    {
        // Arrange
        var scan = CreateTestScan();
        var result = ScanResult.Success(scan);

        // Act
        var returned = result.GetScanOrThrow();

        // Assert
        returned.Should().BeSameAs(scan);
    }

    [Fact]
    public void GetScanOrThrow_OnFailure_ThrowsScanException()
    {
        // Arrange
        var error = new ScanError("TEST_CODE", "Test message");
        var result = ScanResult.Failure(error);

        // Act
        var act = () => result.GetScanOrThrow();

        // Assert
        act.Should().Throw<ScanException>()
            .Where(ex => ex.Error == error)
            .WithMessage("Test message");
    }

    [Fact]
    public void ScanError_PredefinedCodes_AreCorrect()
    {
        // Assert
        ScanError.ReadError.Should().Be("SCAN_READ_ERROR");
        ScanError.InvalidData.Should().Be("SCAN_INVALID_DATA");
        ScanError.Timeout.Should().Be("SCAN_TIMEOUT");
        ScanError.BufferOverflow.Should().Be("SCAN_BUFFER_OVERFLOW");
        ScanError.Cancelled.Should().Be("SCAN_CANCELLED");
        ScanError.Disconnected.Should().Be("SCAN_DISCONNECTED");
    }

    [Fact]
    public void ScanException_ContainsError()
    {
        // Arrange
        var innerException = new InvalidOperationException("inner");
        var error = new ScanError("CODE", "Message", innerException);

        // Act
        var exception = new ScanException(error);

        // Assert
        exception.Error.Should().BeSameAs(error);
        exception.Message.Should().Be("Message");
        exception.InnerException.Should().BeSameAs(innerException);
    }

    private static FrozenOrbitrapScan CreateTestScan()
    {
        return new FrozenOrbitrapScan(
            scanNumber: 42,
            msOrder: 1,
            retentionTime: 10.0,
            mzValues: new[] { 100.0 },
            intensityValues: new[] { 1000.0 },
            basePeakMz: 100.0,
            basePeakIntensity: 1000.0,
            totalIonCurrent: 1000.0,
            precursorMass: null,
            precursorCharge: null,
            precursorIntensity: null,
            isolationWidth: null,
            collisionEnergy: null,
            fragmentationType: null,
            analyzer: "Orbitrap",
            resolutionAtMz200: 120000,
            massAccuracyPpm: 3.0,
            polarity: Polarity.Positive);
    }
}
