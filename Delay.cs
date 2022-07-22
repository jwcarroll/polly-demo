namespace polly_demo
{
    internal class Delay
    {
        private Int32 DelaySecondsLowerBoundInclusive { get; set; } = 0;
        private Int32 DelaySecondsUpperBoundInclusive { get; set; } = 0;

        internal TimeSpan DelayTimeSpan { get; private set; } = TimeSpan.Zero;

        internal void SetFixedDelay(int delay)
        {
            this.SetDelayRange(delay, delay);
        }

        internal void SetDelayRange(int lowerBound, int upperBound)
        {
            if (lowerBound < 0 || upperBound < 0)
            {
                throw new ArgumentException("Delay value must be a positive integer");
            }
            if (lowerBound > upperBound)
            {
                throw new ArgumentException($"{nameof(lowerBound)} cannot be greater than {nameof(upperBound)}, values:{{ lowerBound: {lowerBound}, upperBound: {upperBound} }}");
            }

            DelaySecondsLowerBoundInclusive = lowerBound;
            DelaySecondsUpperBoundInclusive = upperBound;
            DelayTimeSpan = GetDelay(lowerBound, upperBound);
        }

        internal TimeSpan Recalculate()
        {
            DelayTimeSpan = GetDelay(DelaySecondsLowerBoundInclusive, DelaySecondsUpperBoundInclusive);
            return DelayTimeSpan;
        }

        private TimeSpan GetDelay(int lowerBound, int upperBound)
        {
            if (lowerBound == upperBound)
            {
                return TimeSpan.FromSeconds(lowerBound);
            }

            var rand = new Random();

            return TimeSpan.FromSeconds(
                rand.NextInt64(lowerBound, upperBound + 1)
            );
        }
    }
}