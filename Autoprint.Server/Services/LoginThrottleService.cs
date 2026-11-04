using System.Collections.Concurrent;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Ralentit les tentatives de connexion par compte (CWE-307), en complément de la limite par IP.
    /// Pas de verrouillage permanent : un attaquant ne peut bloquer un compte que quelques minutes.
    /// </summary>
    public class LoginThrottleService
    {
        private const int MaxFailures = 5;
        private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(2);

        private readonly ConcurrentDictionary<string, AttemptState> _attempts = new(StringComparer.OrdinalIgnoreCase);

        private class AttemptState
        {
            public int Failures;
            public DateTime FirstFailureUtc;
            public DateTime? LockedUntilUtc;
        }

        public bool IsLocked(string username, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (!_attempts.TryGetValue(Normalize(username), out var state)) return false;

            lock (state)
            {
                if (state.LockedUntilUtc is DateTime until && until > DateTime.UtcNow)
                {
                    remaining = until - DateTime.UtcNow;
                    return true;
                }
                return false;
            }
        }

        public void RegisterFailure(string username)
        {
            var state = _attempts.GetOrAdd(Normalize(username), _ => new AttemptState { FirstFailureUtc = DateTime.UtcNow });
            lock (state)
            {
                var now = DateTime.UtcNow;
                if (now - state.FirstFailureUtc > FailureWindow)
                {
                    state.Failures = 0;
                    state.FirstFailureUtc = now;
                }

                state.Failures++;
                if (state.Failures >= MaxFailures)
                {
                    state.LockedUntilUtc = now.Add(LockDuration);
                    state.Failures = 0;
                    state.FirstFailureUtc = now;
                }
            }

            if (_attempts.Count > 10000) Cleanup();
        }

        public void RegisterSuccess(string username) => _attempts.TryRemove(Normalize(username), out _);

        private static string Normalize(string username) => (username ?? string.Empty).Trim();

        private void Cleanup()
        {
            var now = DateTime.UtcNow;
            foreach (var kv in _attempts)
            {
                bool expiredLock = kv.Value.LockedUntilUtc == null || kv.Value.LockedUntilUtc < now;
                if (expiredLock && now - kv.Value.FirstFailureUtc > FailureWindow) _attempts.TryRemove(kv.Key, out _);
            }
        }
    }
}
