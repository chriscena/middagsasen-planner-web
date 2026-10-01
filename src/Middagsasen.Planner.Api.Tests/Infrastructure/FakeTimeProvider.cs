namespace Middagsasen.Planner.Api.Tests.Infrastructure
{
    /// <summary>TimeProvider med fast «nå» for deterministiske tester.</summary>
    public sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
