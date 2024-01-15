namespace ratelimiter
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RateLimiterDecorator: Attribute
    {
        public StrategyTypeEnum StrategyType { get; set; }
    }
}