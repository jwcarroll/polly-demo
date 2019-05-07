using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Caching;
using Polly.Caching.Memory;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace polly_demo
{
    class Program
    {
        const Int32 NUM_ITERATIONS = 10;
        private static readonly object Parallel;

        static void Main(string[] args)
        {
            ConfigureLogging();

            //RunTransietErrors();
            //RunTransietErrorsManualRetry();
            //RunTransietErrorsWithRetryPolicy();
            //RunTransietErrorsWithLongServiceInteruption();
            //RunExpensiveCallWithSystemDown();
            //RunRecoveryRetryAction();
            //RunRecoveryWithFallback();
            RunExpensiveCallWithCaching();
        }

        static void RunTransietErrors()
        {
            var svc = new EchoService()
                .DelayBy(1)
                .FailAfter(2);

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                try
                {
                    var msg = svc.Echo($"Call:{i}");

                    Log.Information("{Message}", msg);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed on attempt {attemptNum}", i);
                }
            }
        }

        static void RunTransietErrorsManualRetry()
        {
            var svc = new EchoService()
                .DelayBy(1)
                .FailAfter(3);

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                try
                {
                    //Retry Logic
                    var maxTries = 3;

                    for (var numTries = 0; numTries < maxTries; numTries++)
                    {
                        try
                        {
                            var msg = svc.Echo($"Call:{i}");

                            Log.Information("{Message}", msg);

                            break;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("Failed on try {attemptNum}... Retrying...", numTries);

                            if(numTries >= maxTries){
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed on attempt {attemptNum}", i);
                }
            }
        }

        static void RunTransietErrorsWithRetryPolicy()
        {
            var svc = new EchoService()
                .DelayBy(1)
                .FailAfter(2);

            var retry = CreateRetryPolicy<TransientException>();

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                retry.Execute(() =>
                {
                    var msg = svc.Echo($"Call:{i}");

                    Log.Information("{Message}", msg);
                });
            }
        }

        static void RunTransietErrorsWithLongServiceInteruption()
        {
            var svc = new EchoService()
                .DelayBy(1)
                .FailFor(10);

            //var retry = CreateRetryPolicy<TransientException>();
            var retry = CreateExponentialBackoffPolicy();

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                retry.Execute(() =>
                {
                    var msg = svc.Echo($"Call:{i}");

                    Log.Information("{Message}", msg);
                });
            }
        }

        static void RunExpensiveCallWithSystemDown()
        {
            var svc = new EchoService()
                .DelayBy(5)
                .SystemDownAfter(1);

            //var policy = CreateRetryPolicy<SystemDownException>();
            //var policy = CreateCircuitBreakerPolicy();

             var retry = CreateRetryPolicy<SystemDownException>();
             var breaker = CreateCircuitBreakerPolicy();
             var policy = breaker.Wrap(retry);

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                try
                {
                    policy.Execute(() =>
                    {
                        var msg = svc.Echo($"Call:{i}");

                        Log.Information("{Message}", msg);
                    });
                }
                catch (Exception ex)
                {
                    Log.Error("Unable to contact service for attempt {n}", i);
                }
            }
        }

        static void RunRecoveryRetryAction()
        {
            var svc = new EchoService()
                .DelayBy(1)
                .FailUsingProbability(0.7);

            //var retry = CreateRetryPolicy<AlwaysFailException>();

            // var retry = CreateRecoveryPolicy<AlwaysFailException>(() =>
            // {
            //     svc.AlwaysFail = false;
            // });

            var retry = CreateRetryPolicy<TransientException>()
            .Wrap(CreateRecoveryPolicy<AlwaysFailException>(() =>
            {
                svc.AlwaysFail = false;
            }));

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                svc.AlwaysFail = true;

                try
                {
                    retry.Execute(() =>
                    {
                        var msg = svc.Echo($"Call:{i}");

                        Log.Information("{Message}", msg);
                    });
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to echo message on attempt {n}", i);
                }
            }
        }

        static void RunRecoveryWithFallback()
        {
            var svc = new EchoService()
                .DelayBy(1)
                .FailUsingProbability(0.9);

            var retry = CreateRetryPolicy<TransientException>();
            var recovery = CreateRecoveryPolicy<AlwaysFailException>(() =>
            {
                svc.AlwaysFail = false;
            });

            var policy = Policy<String>
                .Handle<TransientException>()
                .Fallback<String>(() => "Default Message")
                .Wrap(retry)
                .Wrap(recovery);

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                svc.AlwaysFail = true;

                var msg = policy.Execute(
                    () => svc.Echo($"Call:{i}")
                );

                Log.Information("{Message}", msg);
            }
        }


        private static void RunExpensiveCallWithCaching()
        {
            var svc = new EchoService()
                .DelayBy(2);

            var policy = CreateCachingPolicy(TimeSpan.FromSeconds(5));

            for (var i = 0; i < (NUM_ITERATIONS * 3); i++)
            {
                var msg = policy.Execute(
                    context => svc.Echo($"Call:{i}"),
                    new Context("EchoCachingMessage")
                );

                Log.Information("{Message}", msg);

                Thread.Sleep(TimeSpan.FromSeconds(0.5));
            }
        }

        private static Policy CreateRecoveryPolicy<T>(Action recoveryAction) where T : Exception
        {
            return Policy
                .Handle<T>()
                .Retry(3, (ex, n) =>
                {
                    Log.Warning("Operation failed on {n} attempt. Execution recovery action...", n);

                    if (recoveryAction != null)
                    {
                        recoveryAction();
                    }
                });
        }

        private static Policy CreateRetryPolicy<T>() where T : Exception
        {
            return Policy
                .Handle<T>()
                .Retry(3, (ex, n) =>
                {
                    Log.Warning("Operation failed on {n} attempt. Retrying...", n);
                });
        }

        private static Policy CreateExponentialBackoffPolicy()
        {
            return Policy
                .Handle<TransientException>()
                .WaitAndRetry(
                    5,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, context) =>
                    {
                        Log.Warning("Operation failed after waiting {n} seconds. Retrying...", timeSpan.TotalSeconds);
                    });
        }

        private static Policy CreateCircuitBreakerPolicy()
        {
            return Policy
                .Handle<SystemDownException>()
                .CircuitBreaker(1, TimeSpan.FromMinutes(1));
        }

        private static CachePolicy CreateCachingPolicy(TimeSpan expiration)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var memoryCacheProvider = new MemoryCacheProvider(memoryCache);

            return Policy.Cache(
                memoryCacheProvider,
                expiration,
                (ctx, key) => Log.Verbose("[CACHE:{key}] - HIT", key),
                (ctx, key) => Log.Verbose("[CACHE:{key}] - MISS", key),
                (ctx, key) => Log.Verbose("[CACHE:{key}] - ADD", key),
                (ctx, key, ex) => Log.Error("[CACHE:{key}] - ERROR", key),
                (ctx, key, ex) => Log.Error("[CACHE:{key}] - ERROR", key));
        }

        static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
                .CreateLogger();
        }
    }
}
