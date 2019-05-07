using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;

namespace polly_demo
{
    public class EchoService
    {
        private Int32 DelayBySeconds { get; set; } = 0;

        private Int32 NumExecutions { get; set; } = 0;

        private Int32 FailAfterNExecutions { get; set; } = 0;

        private Int32 FailForNSeconds { get; set; } = 0;

        private Int32 SystemDownAfterNExecutions { get; set; } = 0;

        public Boolean AlwaysFail { get; set; } = false;

        private Stopwatch Timer { get; set; } = new Stopwatch();

        private Random RandomNumber { get; set; } = new Random();

        private Double RandomFailureProbability { get; set; } = 0.0;

        public String Echo(String msg)
        {
            Delay(DelayBySeconds);
            IncrementExecutions(1);
            HandleAlwaysFailFault();
            HandleExecutionFault();
            HandleTimingFault();
            HandleSystemDownFault();
            HandleRandomFault();

            return msg;
        }

        public EchoService FailUsingProbability(Double probability)
        {
            RandomFailureProbability = probability;
            return this;
        }

        public EchoService SystemDownAfter(Int32 executions)
        {
            SystemDownAfterNExecutions = executions;
            return this;
        }

        public EchoService FailFor(Int32 seconds)
        {
            FailForNSeconds = seconds;
            return this;
        }

        public EchoService DelayBy(Int32 seconds)
        {
            DelayBySeconds = seconds;
            return this;
        }

        public EchoService FailAfter(Int32 executions)
        {
            FailAfterNExecutions = executions;
            return this;
        }

        private void HandleAlwaysFailFault()
        {
            if (AlwaysFail)
            {
                throw new AlwaysFailException();
            }
        }

        private void HandleRandomFault()
        {
            var randomNumber = RandomNumber.NextDouble();

            if (randomNumber <= RandomFailureProbability)
            {
                throw new TransientException($"Random Number: {randomNumber} is below probability threshold of {RandomFailureProbability}");
            }
        }

        private void HandleSystemDownFault()
        {
            if (SystemDownAfterNExecutions == 0)
            {
                return;
            }

            if (NumExecutions >= SystemDownAfterNExecutions)
            {
                throw new SystemDownException($"System down after {SystemDownAfterNExecutions} executions");
            }
        }

        private void HandleTimingFault()
        {
            if (FailForNSeconds <= 0)
            {
                return;
            }

            if (Timer.Elapsed.TotalSeconds < FailForNSeconds)
            {
                throw new TransientException($"Execution Time in Seconds: {Timer.Elapsed.TotalSeconds}");
            }
        }

        private void HandleExecutionFault()
        {
            if (FailAfterNExecutions <= 0)
            {
                return;
            }

            if (NumExecutions % FailAfterNExecutions == 0)
            {
                throw new TransientException($"Failing after {FailAfterNExecutions} executions");
            }
        }

        private void IncrementExecutions(Int32 increment)
        {
            if (NumExecutions == 0)
            {
                Timer.Start();
            }

            NumExecutions += increment;
        }

        private void Delay(int delayBySeconds)
        {
            if (delayBySeconds > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(delayBySeconds));
            }
        }
    }

    [Serializable]
    internal class AlwaysFailException : Exception
    {
        public AlwaysFailException()
        {
        }

        public AlwaysFailException(string message) : base(message)
        {
        }

        public AlwaysFailException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AlwaysFailException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    internal class SystemDownException : Exception
    {
        public SystemDownException()
        {
        }

        public SystemDownException(string message) : base(message)
        {
        }

        public SystemDownException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SystemDownException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    internal class TransientException : Exception
    {
        public TransientException()
        {
        }

        public TransientException(string message) : base(message)
        {
        }

        public TransientException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TransientException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}