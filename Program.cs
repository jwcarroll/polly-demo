using polly_demo;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

ConfigureLogging();

Scenario.RunTransietErrors();
// Scenario.RunTransietErrorsManualRetry();
// Scenario.RunTransietErrorsWithRetryPolicy();
// Scenario.RunTransietErrorsWithLongServiceInteruptionFixedRetries();
// Scenario.RunTransietErrorsWithLongServiceInteruptionExponentialBackoff();
// Scenario.RunExpensiveCallWithSystemDown();
// Scenario.RunExpensiveCallWithSystemDownUsingCircuitBreaker();
// Scenario.RunExpensiveCallWithSystemDownCombinedRetryAndCircuitBreaker();
// Scenario.RunExpensiveCallWithSystemDownAllowCircuitToReOpen();
// Scenario.FailingActionWithSimpleRetry();
// Scenario.FailingActionWithRecoveryPolicy();
// Scenario.FailingActionWithRecoveryAndRetry();
// Scenario.RunRecoveryWithFallback();
// Scenario.RunExpensiveCallWithCaching();

static void ConfigureLogging()
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console(theme: AnsiConsoleTheme.Code)
        .CreateLogger();
}