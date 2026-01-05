using System.Collections.Concurrent;

namespace GatewayService.Services
{
    public class ServiceCircuitBreaker
    {
        private class ServiceState
        {
            public int CurrentRequests { get; set; }
            public DateTime? BreakUntil { get; set; }
        }

        private readonly ConcurrentDictionary<string, ServiceState> _serviceStates = new();
        private readonly int _maxRequestsPerService;
        private readonly int _breakDurationSeconds;
        private readonly object _lock = new object();

        public ServiceCircuitBreaker(int maxRequestsPerService, int breakDurationSeconds)
        {
            _maxRequestsPerService = maxRequestsPerService;
            _breakDurationSeconds = breakDurationSeconds;
        }

        public bool CanMakeRequest(string serviceName)
        {
            var state = GetOrCreateState(serviceName);

            lock (_lock)
            {
                if (state.BreakUntil.HasValue && DateTime.UtcNow < state.BreakUntil.Value)
                {
                    return false;
                }

                if (state.BreakUntil.HasValue && DateTime.UtcNow >= state.BreakUntil.Value)
                {
                    state.BreakUntil = null;
                }

                return state.CurrentRequests < _maxRequestsPerService;
            }
        }

        public bool TryStartRequest(string serviceName)
        {
            var state = GetOrCreateState(serviceName);

            lock (_lock)
            {
                if (state.BreakUntil.HasValue && DateTime.UtcNow < state.BreakUntil.Value)
                {
                    return false;
                }

                if (state.CurrentRequests >= _maxRequestsPerService)
                {
                    state.BreakUntil = DateTime.UtcNow.AddSeconds(_breakDurationSeconds);
                    return false;
                }

                state.CurrentRequests++;
                return true;
            }
        }

        public void EndRequest(string serviceName)
        {
            var state = GetOrCreateState(serviceName);

            lock (_lock)
            {
                state.CurrentRequests--;
                if (state.CurrentRequests < 0)
                    state.CurrentRequests = 0;
            }
        }

        private ServiceState GetOrCreateState(string serviceName)
        {
            return _serviceStates.GetOrAdd(serviceName, _ => new ServiceState());
        }
    }
}