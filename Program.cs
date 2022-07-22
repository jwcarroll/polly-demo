using polly_demo;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

ConfigureLogging();

Scenario.RunTransietErrors();
//Scenario.RunTransietErrorsManualRetry();
//Scenario.RunTransietErrorsWithRetryPolicy();
//Scenario.RunTransietErrorsWithLongServiceInteruption();
//Scenario.RunExpensiveCallWithSystemDown();
//Scenario.RunRecoveryRetryAction();
//Scenario.RunRecoveryWithFallback();
//Scenario.RunExpensiveCallWithCaching();


static void ConfigureLogging()
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console(theme: AnsiConsoleTheme.Code)
        .CreateLogger();
}