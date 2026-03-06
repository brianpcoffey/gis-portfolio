namespace Portfolio.Tests
{
    /// <summary>
    /// Deterministic <see cref="TimeProvider"/> for unit tests.
    /// </summary>
    public sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;
    }
}