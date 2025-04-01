using FluentAssertions;
using TestNest.SmartEnumResult.Domain.Enums;
using TestNest.SmartEnumResult.Domain.Exceptions;

namespace TestNest.SmartEnumResult.Test;

public class CheckInOutTests
{
    private readonly DateTime _now = DateTime.UtcNow;
    private const int AllowedSeconds = 5;

    private DateTime UtcMinValue => DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    #region Creation Tests

    [Fact]
    public void Create_ValidCheckIn_ReturnsCheckInStatus()
    {
        // Arrange
        var checkInTime = _now;
        var checkOutTime = UtcMinValue;

        // Act
        var result = CheckInOut.Create(checkInTime, checkOutTime, CheckInOutStatus.CheckIn);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CheckInOutStatus.CheckIn);
        result.Value.CheckInDateTime.Should().Be(checkInTime);
        result.Value.CheckOutDateTime.Should().Be(checkOutTime);
    }

    [Fact]
    public void Create_ValidCheckOut_ReturnsCheckOutStatus()
    {
        // Arrange
        var checkInTime = DateTime.UtcNow.AddSeconds(-4); // Within 5-second window
        var checkOutTime = DateTime.UtcNow;

        // Act
        var result = CheckInOut.Create(
            checkInTime,
            checkOutTime,
            CheckInOutStatus.CheckOut,
            CheckInOutStatus.CheckIn);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CheckInOutStatus.CheckOut);
        result.Value.CheckInDateTime.Should().Be(checkInTime);
        result.Value.CheckOutDateTime.Should().Be(checkOutTime);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_WithNonUtcDateTime_ThrowsException(DateTimeKind kind)
    {
        // Arrange
        var invalidTime = new DateTime(_now.Ticks, kind);

        // Act
        var result = CheckInOut.Create(invalidTime, UtcMinValue, CheckInOutStatus.CheckIn);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == nameof(CheckInOutException.ErrorCode.NonUtcDateTime));
    }

    [Fact]
    public void Create_CheckInTooFarInFuture_ThrowsException()
    {
        // Arrange
        var futureTime = _now.AddYears(2);
        var checkOutTime = UtcMinValue;

        // Act
        var result = CheckInOut.Create(futureTime, checkOutTime, CheckInOutStatus.CheckIn);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == nameof(CheckInOutException.ErrorCode.FutureCheckInTooFar));
    }

    [Fact]
    public void Create_CheckInTooFarInPast_ThrowsException()
    {
        // Arrange
        var pastTime = _now.AddSeconds(-AllowedSeconds - 1);
        var checkOutTime = UtcMinValue;

        // Act
        var result = CheckInOut.Create(pastTime, checkOutTime, CheckInOutStatus.CheckIn);

        // Act & Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == nameof(CheckInOutException.ErrorCode.PastCheckInNotAllowed));
        //Assert.Throws<CheckInOutException>(() =>
        //    CheckInOut.Create(pastTime, checkOutTime, CheckInOutStatus.CheckIn));
    }

    #endregion Creation Tests

    #region Transition Tests

    [Fact]
    public void Transition_FromNoneToCheckIn_Success()
    {
        // Arrange
        var initial = CheckInOut.Empty;
        var timestamp = DateTime.UtcNow; // Fresh UTC timestamp

        // Act
        var result = initial.TransitionTo(CheckInOutStatus.CheckIn, timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CheckInOutStatus.CheckIn);
        result.Value.CheckInDateTime.Should().Be(timestamp);
        result.Value.CheckOutDateTime.Should().Be(UtcMinValue);
    }

    [Fact]
    public void Transition_FromCheckInToCheckOut_Success()
    {
        // Arrange
        var checkInTime = DateTime.UtcNow.AddSeconds(-4);
        var checkOutTime = DateTime.UtcNow;

        var initialResult = CheckInOut.Create(
            checkInTime,
            UtcMinValue,
            CheckInOutStatus.CheckIn);

        // Ensure initial creation was successful
        initialResult.IsSuccess.Should().BeTrue();
        var initial = initialResult.Value;

        // Act
        var transitionResult = initial!.TransitionTo(
            CheckInOutStatus.CheckOut,
            checkOutTime);

        // Assert
        transitionResult.IsSuccess.Should().BeTrue();
        var result = transitionResult.Value;
        result!.Status.Should().Be(CheckInOutStatus.CheckOut);
        result.CheckInDateTime.Should().Be(checkInTime);
        result.CheckOutDateTime.Should().Be(checkOutTime);
    }

    [Fact]
    public void Transition_InvalidStateChange_ThrowsException()
    {
        // Arrange
        var initialResult = CheckInOut.Create(_now, UtcMinValue, CheckInOutStatus.CheckIn);

        // Ensure initial creation was successful
        initialResult.IsSuccess.Should().BeTrue();
        var initial = initialResult.Value;

        // Act
        var transitionResult = initial!.TransitionTo(CheckInOutStatus.None, _now);

        // Assert
        transitionResult.IsSuccess.Should().BeFalse();
        transitionResult.Errors.Should().Contain(e => e.Code == nameof(CheckInOutException.ErrorCode.InvalidStatusTransition));
    }

    #endregion Transition Tests

    #region Method Tests

    [Fact]
    public void GetDuration_ForCheckOut_ReturnsCorrectDuration()
    {
        // Arrange
        var checkInTime = DateTime.UtcNow;
        var checkOutTime = checkInTime.AddSeconds(4); // Exact 4 second difference

        var result = CheckInOut.Create(
            checkInTime,
            checkOutTime,
            CheckInOutStatus.CheckOut,
            CheckInOutStatus.CheckIn);

        // Ensure creation was successful
        result.IsSuccess.Should().BeTrue();
        var co = result.Value;

        // Act
        var duration = co!.GetDuration();

        // Assert
        duration.TotalSeconds.Should().BeApproximately(4, 0); // Whole seconds only
    }

    //[Fact]
    public void GetDuration_ForNonCheckOut_ReturnsZero()
    {
        // Arrange
        var result = CheckInOut.Create(_now, UtcMinValue, CheckInOutStatus.CheckIn);

        // Ensure creation was successful
        result.IsSuccess.Should().BeTrue();
        var co = result.Value;

        // Act
        var duration = co.GetDuration();

        // Assert
        duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void IsActive_ForValidCheckIn_ReturnsTrue()
    {
        // Arrange
        var validCheckInTime = DateTime.UtcNow.AddSeconds(-4); // Within 5-second window
        var result = CheckInOut.Create(
            validCheckInTime,
            UtcMinValue,
            CheckInOutStatus.CheckIn);

        // Ensure creation was successful
        result.IsSuccess.Should().BeTrue();
        var co = result.Value;

        // Act & Assert
        co.IsActive().Should().BeTrue();
    }

    [Fact]
    public void IsActive_ForFutureCheckIn_ReturnsFalse()
    {
        // Arrange
        var result = CheckInOut.Create(_now.AddMinutes(5), UtcMinValue, CheckInOutStatus.CheckIn);

        // Ensure creation was successful
        result.IsSuccess.Should().BeTrue();
        var co = result.Value;

        // Act & Assert
        co.IsActive().Should().BeFalse();
    }

    #endregion Method Tests

    #region Update Tests

    [Fact]
    public void Update_ChangesValuesCorrectly()
    {
        // Arrange
        // Original check-in within 5-second window
        var originalCheckIn = DateTime.UtcNow.AddSeconds(-4);
        var originalResult = CheckInOut.Create(
            originalCheckIn,
            UtcMinValue,
            CheckInOutStatus.CheckIn
        );

        // Ensure original creation was successful
        originalResult.IsSuccess.Should().BeTrue();
        var original = originalResult.Value;

        // New check-in still within 5-second window
        var newCheckIn = DateTime.UtcNow.AddSeconds(-2);
        var checkOutTime = DateTime.UtcNow;

        // Act
        var updatedResult = original.Update(
            newCheckIn,
            checkOutTime,
            CheckInOutStatus.CheckOut
        );

        // Ensure update was successful
        updatedResult.IsSuccess.Should().BeTrue();
        var updated = updatedResult.Value;

        // Assert
        updated.CheckInDateTime.Should().Be(newCheckIn);
        updated.CheckOutDateTime.Should().Be(checkOutTime);
        updated.Status.Should().Be(CheckInOutStatus.CheckOut);
    }

    [Fact]
    public void Update_LeavesOriginalInstanceUnchanged()
    {
        // Arrange
        // Create original instance with valid check-in time
        var originalCheckIn = DateTime.UtcNow.AddSeconds(-4); // Within 5-second window
        var originalResult = CheckInOut.Create(
            originalCheckIn,
            UtcMinValue,
            CheckInOutStatus.CheckIn,
            previousStatus: CheckInOutStatus.None // Transition from "None"
        );

        // Ensure original creation was successful
        originalResult.IsSuccess.Should().BeTrue();
        var original = originalResult.Value;

        // New check-in time still within valid window
        var newCheckIn = DateTime.UtcNow.AddSeconds(-2);
        var checkOutTime = DateTime.UtcNow;

        // Act
        var updatedResult = original.Update(newCheckIn, checkOutTime, CheckInOutStatus.CheckOut);

        // Ensure update was successful
        updatedResult.IsSuccess.Should().BeTrue();
        var updated = updatedResult.Value;

        // Assert
        updated.CheckInDateTime.Should().NotBe(original.CheckInDateTime);
        updated.Status.Should().NotBe(original.Status);
    }

    #endregion Update Tests

    #region Edge Cases

    [Fact]
    public void Empty_HasCorrectDefaultValues()
    {
        // Act
        var empty = CheckInOut.Empty;

        // Assert
        empty.CheckInDateTime.Should().Be(UtcMinValue);
        empty.CheckOutDateTime.Should().Be(UtcMinValue);
        empty.Status.Should().Be(CheckInOutStatus.None);
        empty.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ToString_ForCheckIn_ReturnsFormattedString()
    {
        // Arrange
        var result = CheckInOut.Create(_now, UtcMinValue, CheckInOutStatus.CheckIn);

        // Ensure creation was successful
        result.IsSuccess.Should().BeTrue();
        var co = result.Value;

        // Act
        var resultString = co.ToString();

        // Assert
        resultString.Should().Contain(_now.ToString("u"));
        resultString.Should().Contain("Checked in");
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var result1 = CheckInOut.Create(_now, UtcMinValue, CheckInOutStatus.CheckIn);
        var result2 = CheckInOut.Create(_now, UtcMinValue, CheckInOutStatus.CheckIn);

        // Ensure both creations were successful
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        var co1 = result1.Value;
        var co2 = result2.Value;

        // Act & Assert
        co1.Should().Be(co2);
        (co1 == co2).Should().BeTrue();
    }

    #endregion Edge Cases

    #region Validation Tests

    [Fact]
    public void Create_CheckOutWithoutCheckIn_ThrowsException()
    {
        // Act
        var result = CheckInOut.Create(_now, _now.AddHours(1), CheckInOutStatus.CheckOut);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == nameof(CheckInOutException.ErrorCode.CheckInRequiredBeforeCheckOut));
    }

    [Fact]
    public void Create_CheckOutBeforeCheckIn_ThrowsException()
    {
        // Arrange
        var checkOut = _now.AddHours(-1);

        // Act
        var result = CheckInOut.Create(_now, checkOut, CheckInOutStatus.CheckOut, CheckInOutStatus.CheckIn);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == nameof(CheckInOutException.ErrorCode.InvalidDateRange));
    }

    [Fact]
    public void Create_InvalidNoneState_ThrowsException()
    {
        // Arrange
        var checkIn = _now;
        var checkOut = UtcMinValue;

        // Act
        var result = CheckInOut.Create(checkIn, checkOut, CheckInOutStatus.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == nameof(CheckInOutException.ErrorCode.InvalidNoneState));
    }

    #endregion Validation Tests
}