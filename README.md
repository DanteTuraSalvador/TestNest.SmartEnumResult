# 🚀 SmartEnum with Result Pattern
This project demonstrates how to combine the SmartEnum library with the Result pattern in .NET applications to provide type-safe enums and consistent handling of operation outcomes.

By using SmartEnum for type-safe enums and the Result pattern for outcome handling, the project ensures clear, maintainable, and scalable code that adheres to clean architecture principles.

## Features
- 🔒 **SmartEnum**: Leverage the type-safety and extensibility of SmartEnum for enum values, reducing the risk of errors and improving code readability.
- 🔄 **Result Pattern**: A flexible pattern to handle success, failure, and business logic outcomes in a unified way, making code more predictable and consistent.
- 🧪 **Test Cases and Examples**: Includes practical examples and test cases demonstrating the use of SmartEnum and Result pattern in action.

## Why Use SmartEnum with Result Pattern?
- 🔐 **Type-Safe Enums**: SmartEnum allows for safer and more readable enums, especially when the enum values have associated behaviors or are not simple flag types.
- ✅ **Consistent Error Handling**: The Result pattern provides a standard way to communicate operation results, making it easier to handle success and failure in a uniform way.
- 🔤 **Reduced Primitive Types**: With SmartEnum and Result, the need for using primitive types like `int`, `string`, or `Guid` in your business logic is minimized, leading to more expressive and self-documenting code.

## 📌 Core Implementation

### 🔹 CheckInOut Value Object with Result Pattern 
See the Result pattern: [https://github.com/DanteTuraSalvador/TestNest.ResultPatterns](https://github.com/DanteTuraSalvador/TestNest.ResultPatterns)<br>
See the Value Object: [https://github.com/DanteTuraSalvador/TestNest.ValueObjects](https://github.com/DanteTuraSalvador/TestNest.ValueObjects)<br>
See the Smart Enums: [https://github.com/DanteTuraSalvador/TestNest.SmartEnums](https://github.com/DanteTuraSalvador/TestNest.SmartEnums)<br>
        
```csharp
public sealed class CheckInOut : ValueObject
{
    private static readonly Lazy<CheckInOut> _empty = new(() => new CheckInOut());
    public static CheckInOut Empty => _empty.Value;
    public bool IsEmpty => this == Empty;

    public DateTime CheckInDateTime { get; }
    public DateTime CheckOutDateTime { get; }
    public CheckInOutStatus Status { get; }

    private CheckInOut() => (CheckInDateTime, CheckOutDateTime, Status) =
        (DateTime.MinValue, DateTime.MinValue, CheckInOutStatus.None);

    private CheckInOut(DateTime checkIn, DateTime checkOut, CheckInOutStatus status)
    {
        CheckInDateTime = checkIn;
        CheckOutDateTime = checkOut;
        Status = status;
    }

    public static Result<CheckInOut> Create(
        DateTime checkIn,
        DateTime checkOut,
        CheckInOutStatus status,
        CheckInOutStatus? previousStatus = null)
    {
        var dateTimeKindResult = ValidateDateTimeKind(checkIn, checkOut);
        if (!dateTimeKindResult.IsSuccess)
            return Result<CheckInOut>.Failure(dateTimeKindResult.ErrorType, dateTimeKindResult.Errors);

        var statusRulesResult = ValidateStatusRules(checkIn, checkOut, status, previousStatus);
        if (!statusRulesResult.IsSuccess)
            return Result<CheckInOut>.Failure(statusRulesResult.ErrorType, statusRulesResult.Errors);

        return Result<CheckInOut>.Success(new CheckInOut(checkIn, checkOut, status));
    }

    private static Result ValidateDateTimeKind(DateTime checkIn, DateTime checkOut)
    {
        if (checkIn.Kind != DateTimeKind.Utc || checkOut.Kind != DateTimeKind.Utc)
        {
            var exception = CheckInOutException.NonUtcDateTime();
            return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
        }

        return Result.Success();
    }

    private static Result ValidateStatusRules(
        DateTime checkIn,
        DateTime checkOut,
        CheckInOutStatus status,
        CheckInOutStatus? previousStatus)
    {
        switch (status)
        {
            case CheckInOutStatus.CheckIn:
                return ValidateCheckIn(checkIn, previousStatus);

            case CheckInOutStatus.CheckOut:
                return ValidateCheckOut(checkIn, checkOut, previousStatus);

            case CheckInOutStatus.None:
                if (checkIn != DateTime.MinValue || checkOut != DateTime.MinValue)
                {
                    var exception = CheckInOutException.InvalidNoneState();
                    return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
                }
                break;

            default:
                var invalidStatusException = CheckInOutException.InvalidStatus();
                return Result.Failure(ErrorType.Validation, invalidStatusException.Code.ToString(), invalidStatusException.Message.ToString());
        }

        return Result.Success();
    }

    private static Result ValidateCheckIn(DateTime checkIn, CheckInOutStatus? previousStatus)
    {
        var now = DateTime.UtcNow;

        if (checkIn > now.AddYears(1))
        {
            var exception = CheckInOutException.FutureCheckInTooFar();
            return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
        }

        if (previousStatus == null && checkIn < now.AddSeconds(-5))
        {
            var exception = CheckInOutException.PastCheckInNotAllowed();
            return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
        }

        return Result.Success();
    }

    private static Result ValidateCheckOut(
        DateTime checkIn,
        DateTime checkOut,
        CheckInOutStatus? previousStatus)
    {
        var now = DateTime.UtcNow;

        if (previousStatus != CheckInOutStatus.CheckIn)
        {
            var exception = CheckInOutException.CheckInRequiredBeforeCheckOut();
            return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
        }

        if (checkOut <= checkIn)
        {
            var exception = CheckInOutException.InvalidDateRange();
            return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
        }

        if (checkIn > now)
        {
            var exception = CheckInOutException.InvalidStatusTransition();
            return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
        }

        if (checkIn < now.AddSeconds(-5))
        {
            var exception = CheckInOutException.StaleCheckIn();
            return Result.Failure(ErrorType.Validation, exception.Code.ToString(), exception.Message.ToString());
        }

        return Result.Success();
    }

    public Result<CheckInOut> TransitionTo(CheckInOutStatus newStatus, DateTime timestamp)
    {
        return (Status, newStatus) switch
        {
            // None → CheckIn
            (CheckInOutStatus.None, CheckInOutStatus.CheckIn) =>
                Create(
                    timestamp,
                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                    newStatus
                ),

            // CheckIn → CheckOut
            (CheckInOutStatus.CheckIn, CheckInOutStatus.CheckOut) =>
                Create(
                    CheckInDateTime,
                    timestamp,
                    newStatus,
                    previousStatus: Status
                ),

            // CheckOut → None
            (CheckInOutStatus.CheckOut, CheckInOutStatus.None) =>
                Create(
                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                    newStatus
                ),

            _ => Result<CheckInOut>.Failure(ErrorType.Validation, CheckInOutException.InvalidStatusTransition().Code.ToString(), CheckInOutException.InvalidStatusTransition().Message.ToString())
        };
    }

    public TimeSpan GetDuration()
    {
        return Status == CheckInOutStatus.CheckOut
            ? CheckOutDateTime - CheckInDateTime
            : TimeSpan.Zero;
    }

    public bool IsActive()
    {
        return Status == CheckInOutStatus.CheckIn
            && CheckInDateTime <= DateTime.UtcNow
            && CheckInDateTime > DateTime.MinValue;
    }

    public Result<CheckInOut> Update(DateTime newCheckIn, DateTime newCheckOut, CheckInOutStatus newStatus)
        => Create(newCheckIn, newCheckOut, newStatus, Status);

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return CheckInDateTime;
        yield return CheckOutDateTime;
        yield return Status;
    }

    public override string ToString()
    {
        return Status switch
        {
            CheckInOutStatus.None => "No check-in recorded",
            CheckInOutStatus.CheckIn => $"Checked in at {CheckInDateTime:u}",
            CheckInOutStatus.CheckOut => $"Checked out at {CheckOutDateTime:u} (Duration: {GetDuration():hh\\:mm})",
            _ => "Invalid status"
        };
    }
}

public enum CheckInOutStatus
{
    None,
    CheckIn,
    CheckOut
}

```
