using System;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Caching;
using Polly.Caching.Memory;
using Serilog;

namespace polly_demo
{
    public static class Scenario
    {
        const Int32 NUM_ITERATIONS = 10;

        /// <summary>
        /// Scenario: A service that demonstrates failures that are routine.
        /// 
        /// Solution: No error handling for demo purposes.
        /// </summary>
        public static void RunTransietErrors()
        {
            var svc = new EchoService()
                .FixedDelay(1)
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

        public static void RunTransietErrorsManualRetry()
        {
            var svc = new EchoService()
                .FixedDelay(1)
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
                        catch (Exception)
                        {
                            Log.Warning("Call:{i} - Failed on try {attemptNum}... Retrying...", i, numTries);

                            if (numTries >= maxTries)
                            {
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

        public static void RunTransietErrorsWithRetryPolicy()
        {
            var svc = new EchoService()
                .FixedDelay(1)
                .FailAfter(3);

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

        public static void RunTransietErrorsWithLongServiceInteruption()
        {
            var svc = new EchoService()
                .RandomDelay(0, 1)
                .FailForRandomTime(5, 15);

            //var retry = CreateRetryPolicy<TransientException>(10);
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

        public static void RunExpensiveCallWithSystemDown()
        {
            var svc = new EchoService()
                .FixedDelay(5)
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
                catch (Exception)
                {
                    Log.Error("Unable to contact service for attempt {n}", i);
                }
            }
        }

        public static void RunRecoveryRetryAction()
        {
            var svc = new EchoService()
                .FixedDelay(1)
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
                catch (Exception)
                {
                    Log.Error("Failed to echo message on attempt {n}", i);
                }
            }
        }

        public static void RunRecoveryWithFallback()
        {
            var svc = new EchoService()
                .FixedDelay(1)
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

        public static void RunExpensiveCallWithCaching()
        {
            var svc = new EchoService()
                .FixedDelay(2);

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


        static Policy CreateRecoveryPolicy<T>(Action recoveryAction) where T : Exception
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

        static Policy CreateRetryPolicy<T>(int numRetries = 3) where T : Exception
        {
            return Policy
                .Handle<T>()
                .Retry(numRetries, (ex, n) =>
                {
                    Log.Warning("Operation failed on {n} attempt. Retrying...", n);
                });
        }

        static Policy CreateExponentialBackoffPolicy()
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

        static Policy CreateCircuitBreakerPolicy()
        {
            return Policy
                .Handle<SystemDownException>()
                .CircuitBreaker(1, TimeSpan.FromMinutes(1));
        }

        static CachePolicy CreateCachingPolicy(TimeSpan expiration)
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

    }
}