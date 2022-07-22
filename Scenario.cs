using System;
using System.Diagnostics;
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

        /// <summary>
        /// Scenario: A service that demonstrates failures that are routine.
        /// 
        /// Solution: Manually written retry logic
        /// </summary>
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

        /// <summary>
        /// Scenario: A service that demonstrates failures that are routine.
        /// 
        /// Solution: Polly retry policy using a fixed number of retries.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
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

        /// <summary>
        /// Scenario: A service that demonstrates intermitent 
        /// failures that last for a random amount of time.
        /// 
        /// Solution: Polly retry policy using a fixed number of retries.
        /// 
        /// Issue: Fixed retries don't take into account longer service
        /// interruptions.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
        public static void RunTransietErrorsWithLongServiceInteruptionFixedRetries()
        {
            var svc = new EchoService()
                .FixedDelay(1)
                .FailForRandomTime(2, 5);

            var retry = CreateRetryPolicy<TransientException>(3);

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                retry.Execute(() =>
                {
                    var msg = svc.Echo($"Call:{i}");

                    Log.Information("{Message}", msg);
                });
            }
        }

        /// <summary>
        /// Scenario: A service that demonstrates intermitent 
        /// failures that last for a random amount of time.
        /// 
        /// Solution: Polly retry policy using a retries with exponential backoff. 
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#wait-and-retry">Wait and Retry</see>
        public static void RunTransietErrorsWithLongServiceInteruptionExponentialBackoff()
        {
            var svc = new EchoService()
                .FixedDelay(1)
                .FailForRandomTime(10, 15);

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

        /// <summary>
        /// Scenario: A service that is down, but takes a long time
        /// before responding with a failure code. (e.g. 504 Gateway Timeout)
        /// 
        /// Solution: Polly retry policy using a fixed number of retries.
        /// 
        /// Issue: We are unlikely to be able to recover, so retries are
        /// just eating up system resources and taking a long time to fail.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
        public static void RunExpensiveCallWithSystemDown()
        {
            var svc = new EchoService()
                .FixedDelay(5)
                .SystemDownAfter(1);

            var policy = CreateRetryPolicy<SystemDownException>();

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

        /// <summary>
        /// Scenario: A service that is down, but takes a long time
        /// before responding with a failure code. (e.g. 504 Gateway Timeout)
        /// 
        /// Solution: Polly retry policy using a circuit breaker to prevent
        /// further calls.
        /// 
        /// Issue: Prevents further calls from eating up system resources,
        /// but there is no way to recover.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
        public static void RunExpensiveCallWithSystemDownUsingCircuitBreaker()
        {
            var svc = new EchoService()
                .FixedDelay(5)
                .SystemDownAfter(1);

            var policy = CreateCircuitBreakerPolicy();

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

        /// <summary>
        /// Scenario: A service that is down, but takes a long time
        /// before responding with a failure code. (e.g. 504 Gateway Timeout)
        /// 
        /// Solution: Combines retries and circuit breaker into a single policy
        /// for a more robust solution.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
        public static void RunExpensiveCallWithSystemDownCombinedRetryAndCircuitBreaker()
        {
            var svc = new EchoService()
                .FixedDelay(5)
                .SystemDownAfter(1);

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

        /// <summary>
        /// Scenario: A service that is down, but takes a long time
        /// before responding with a failure code. (e.g. 504 Gateway Timeout)
        /// 
        /// Solution: Circuit breaker demonstrating how the circuit will
        /// re-open after a certain period of time.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
        public static void RunExpensiveCallWithSystemDownAllowCircuitToReOpen()
        {
            var svc = new EchoService()
                .FixedDelay(5)
                .SystemDownForFixedTime(30);

            var retry = CreateRetryPolicy<SystemDownException>();
            var breaker = CreateCircuitBreakerPolicy();
            var policy = breaker.Wrap(retry);
            
            DoWork(svc, policy);
            WaitFor(TimeSpan.FromSeconds(5));
            DoWork(svc, policy);
            WaitFor(TimeSpan.FromSeconds(5));
            DoWork(svc, policy);

            static void DoWork(EchoService svc, Polly.Wrap.PolicyWrap policy)
            {
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
        }

        /// <summary>
        /// Scenario: A service that is down, and will not recover
        /// without some sort of action being taken.
        /// 
        /// Solution: Simple retry policy
        /// 
        /// Issue: Steps must be taken before the action can be retried
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>

        public static void FailingActionWithSimpleRetry()
        {
            var svc = new EchoService()
                .FixedDelay(1);

            var retry = CreateRetryPolicy<AlwaysFailException>();

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

        /// <summary>
        /// Scenario: A service that is down, and will not recover
        /// without some sort of action being taken.
        /// 
        /// Solution: Recovery policy
        /// 
        /// Issue: Intermittend errors may still cause the operation to fail
        /// after recovery actions were taken.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>

        public static void FailingActionWithRecoveryPolicy()
        {
            var svc = new EchoService()
                .FixedDelay(1)
                .FailAfter(3);

            var policy = CreateRecoveryPolicy<AlwaysFailException>(() =>
            {
                svc.AlwaysFail = false;
            });

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                svc.AlwaysFail = true;

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
                    Log.Error("Failed to echo message on attempt {n}", i);
                }
            }
        }

        /// <summary>
        /// Scenario: A service that is down, and will not recover
        /// without some sort of action being taken.
        /// 
        /// Solution: Recovery policy wrapped with retry
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>

        public static void FailingActionWithRecoveryAndRetry()
        {
            var svc = new EchoService()
                .FixedDelay(1)
                .FailAfter(3);

            var policy = CreateRetryPolicy<TransientException>()
            .Wrap(CreateRecoveryPolicy<AlwaysFailException>(() =>
            {
                svc.AlwaysFail = false;
            }));

            for (var i = 0; i < NUM_ITERATIONS; i++)
            {
                svc.AlwaysFail = true;

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
                    Log.Error("Failed to echo message on attempt {n}", i);
                }
            }
        }

        /// <summary>
        /// Scenario: Service where failure is almost certain, even after
        /// attempting to recover from known issues.
        /// 
        /// Solution: Recovery policy wrapped with retry, combined with a fallback.
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
        public static void RunRecoveryWithFallback()
        {
            var svc = new EchoService()
                .FixedDelay(1)
                .FailUsingProbability(0.75);

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

        /// <summary>
        /// Scenario: A very epensive call is being made, but the value
        /// is not expected to change.
        /// 
        /// Solution: Caching policy
        /// </summary>
        /// <see href="https://github.com/App-vNext/Polly#retry">Retry Policy</see>
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
                .CircuitBreaker(
                    1,
                    TimeSpan.FromSeconds(10),
                    (ex, ts) => Log.Warning("Circuit broken and will remain closed for {n} seconds", ts.Seconds),
                    () => Log.Information("Circuit is has been re-opened")
                    );
        }

        static CachePolicy CreateCachingPolicy(TimeSpan expiration)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var memoryCacheProvider = new MemoryCacheProvider(memoryCache);

            return Policy.Cache(
                memoryCacheProvider,
                expiration,
                (ctx, key) => Log.Verbose("[CACHE:{key}] - HIT", key),
                (ctx, key) => Log.Warning("[CACHE:{key}] - MISS", key),
                (ctx, key) => Log.Verbose("[CACHE:{key}] - ADD", key),
                (ctx, key, ex) => Log.Error("[CACHE:{key}] - ERROR", key),
                (ctx, key, ex) => Log.Error("[CACHE:{key}] - ERROR", key));
        }

        private static void WaitFor(TimeSpan timeSpan)
        {
            var sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < timeSpan.TotalSeconds; i++)
            {
                Log.Information("Waiting for {elapsed:N0} of {total} seconds", sw.Elapsed.TotalSeconds, timeSpan.TotalSeconds);
                Thread.Sleep(1000);
            }

            sw.Stop();
        }
    }
}