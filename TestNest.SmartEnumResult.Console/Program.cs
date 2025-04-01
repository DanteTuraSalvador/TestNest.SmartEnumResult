using TestNest.SmartEnumResult.Domain.Enums;
using TestNest.SmartEnumResult.Domain.Common;
using static System.Console;

class Program
{
    static void Main()
    {
        WriteLine("====== CheckInOut Class Demonstration ======\n");

        DemonstrateValidCheckIn();
        DemonstrateInvalidCheckInTimeZone();
        DemonstrateValidCheckOutWorkflow();
        DemonstrateStaleCheckInCheckOut();
        DemonstrateInvalidStatusTransition();

        WriteLine("\n====== Demonstration Complete ======");

        ReadKey();
    }

    static void DisplayTestHeader(string testTitle, string testDescription)
    {
        ForegroundColor = ConsoleColor.Yellow;
        WriteLine($"\n[TEST CASE] {testTitle}");
        ResetColor();
        WriteLine(testDescription);
    }

    static void DisplayResult(Result<CheckInOut> result)
    {
        if (result.IsSuccess)
        {
            ForegroundColor = ConsoleColor.Green;
            WriteLine("✅ Operation Successful");
            WriteLine($"State: {result.Value}");
        }
        else
        {
            ForegroundColor = ConsoleColor.Red;
            WriteLine("❌ Operation Failed");
            WriteLine("Errors:");
            foreach (var error in result.Errors)
            {
                WriteLine($"- {error.Code}: {error.Message}");
            }
        }
        ResetColor();
        WriteLine(new string('=', 60));
    }

    static void DemonstrateValidCheckIn()
    {
        DisplayTestHeader(
            "Valid Check-In Creation",
            "Test: Creating a check-in with valid parameters\n" +
            "Expected: Successfully create CheckIn status\n" +
            "Conditions:\n" +
            "- Check-in time is UTC\n" +
            "- Within allowed 5-second past window\n" +
            "- Proper initial status transition (None → CheckIn)");

        // Recent check-in (2 seconds ago)
        var checkIn = DateTime.UtcNow.AddSeconds(-2);
        var checkOut = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

        WriteLine($"\nAttempting check-in at {checkIn:u}");
        var result = CheckInOut.Create(checkIn, checkOut, CheckInOutStatus.CheckIn);

        DisplayResult(result);
    }

    static void DemonstrateInvalidCheckInTimeZone()
    {
        DisplayTestHeader(
            "Invalid Check-In Time Zone",
            "Test: Creating check-in with local time instead of UTC\n" +
            "Expected: Validation failure\n" +
            "Error Code: NonUtcDateTime");

        var localCheckIn = DateTime.Now;  // Invalid local time
        var utcCheckOut = DateTime.UtcNow;

        WriteLine($"\nAttempting check-in with:\n" +
                 $"Local time: {localCheckIn:u}\n" +
                 $"UTC check-out: {utcCheckOut:u}");

        var result = CheckInOut.Create(localCheckIn, utcCheckOut, CheckInOutStatus.CheckIn);
        DisplayResult(result);
    }

    static void DemonstrateValidCheckOutWorkflow()
    {
        DisplayTestHeader(
            "Valid Check-Out Workflow",
            "Test: Complete check-in/check-out workflow\n" +
            "Expected: Successful status transitions\n" +
            "Steps:\n" +
            "1. Create valid check-in\n" +
            "2. Transition to check-out\n" +
            "3. Verify duration calculation");

        // Step 1: Create CheckIn
        var checkInTime = DateTime.UtcNow.AddMinutes(-5);
        var checkInResult = CheckInOut.Create(
            checkInTime,
            DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            CheckInOutStatus.CheckIn
        );

        if (!checkInResult.IsSuccess)
        {
            DisplayResult(checkInResult);
            return;
        }

        WriteLine($"\nStep 1: Check-In Created at {checkInTime:u}");
        WriteLine(checkInResult.Value);

        // Step 2: Transition to CheckOut
        var checkOutTime = DateTime.UtcNow;
        WriteLine($"\nStep 2: Attempting check-out at {checkOutTime:u}");
        var checkOutResult = checkInResult.Value.TransitionTo(
            CheckInOutStatus.CheckOut,
            checkOutTime
        );

        DisplayResult(checkOutResult);

        // Step 3: Show duration if successful
        if (checkOutResult.IsSuccess)
        {
            WriteLine($"Work Duration: {checkOutResult.Value.GetDuration()}");
            WriteLine(new string('=', 60));
        }
    }

    static void DemonstrateStaleCheckInCheckOut()
    {
        DisplayTestHeader(
            "Stale Check-In Check-Out",
            "Test: Trying to check-out from an old check-in\n" +
            "Expected: Validation failure\n" +
            "Error Code: StaleCheckIn\n" +
            "Condition: Check-in older than 5 seconds");

        // Create stale check-in (6 seconds old)
        var staleCheckIn = DateTime.UtcNow.AddSeconds(-6);
        WriteLine($"\nCreating stale check-in at {staleCheckIn:u}");

        var checkInResult = CheckInOut.Create(
            staleCheckIn,
            DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            CheckInOutStatus.CheckIn
        );

        // Check if check-in creation succeeded (it shouldn't)
        if (!checkInResult.IsSuccess)
        {
            WriteLine("Check-in creation failed as expected:");
            DisplayResult(checkInResult);
            return;
        }

        // Attempt check-out (shouldn't reach here)
        WriteLine($"Attempting check-out at {DateTime.UtcNow:u}");
        var checkOutResult = checkInResult.Value.TransitionTo(
            CheckInOutStatus.CheckOut,
            DateTime.UtcNow
        );

        DisplayResult(checkOutResult);
    }

    static void DemonstrateInvalidStatusTransition()
    {
        DisplayTestHeader(
            "Invalid Status Transition",
            "Test: Illegal transition from CheckOut → CheckIn\n" +
            "Expected: Validation failure\n" +
            "Error Code: InvalidStatusTransition\n" +
            "Condition: Direct transition from CheckOut to CheckIn");

        // Create valid CheckOut state
        var checkInTime = DateTime.UtcNow.AddHours(-1);
        var checkOutTime = DateTime.UtcNow.AddMinutes(-30);

        WriteLine($"\nCreating initial CheckOut state:\n" +
                 $"Check-in: {checkInTime:u}\n" +
                 $"Check-out: {checkOutTime:u}");

        var checkOutResult = CheckInOut.Create(
            checkInTime,
            checkOutTime,
            CheckInOutStatus.CheckOut,
            CheckInOutStatus.CheckIn
        );

        if (!checkOutResult.IsSuccess)
        {
            DisplayResult(checkOutResult);
            return;
        }

        // Attempt invalid transition
        WriteLine($"\nAttempting invalid transition to CheckIn at {DateTime.UtcNow:u}");
        var invalidTransitionResult = checkOutResult.Value.TransitionTo(
            CheckInOutStatus.CheckIn,
            DateTime.UtcNow
        );

        DisplayResult(invalidTransitionResult);
    }
}