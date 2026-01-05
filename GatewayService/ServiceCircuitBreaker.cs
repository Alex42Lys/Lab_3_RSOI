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

        public bool HasTimeOutPassed(string serviceName)
        {
            var state = GetOrCreateState(serviceName);

            lock (_lock)
            {
                if (state.BreakUntil.HasValue && DateTime.UtcNow < state.BreakUntil.Value)
                {
                    return false;
                }
                else
                    return true;

            }
        }

        public void AddRequest(string serviceName)
        {
            var state = GetOrCreateState(serviceName);

            lock (_lock)
            {
                if (state.CurrentRequests >= _maxRequestsPerService)
                {
                    state.BreakUntil = DateTime.UtcNow.AddSeconds(_breakDurationSeconds);
                }

                state.CurrentRequests++;
            }
        }

        public void Reset(string serviceName)
        {
            var state = GetOrCreateState(serviceName);

            lock (_lock)
            {
                    state.CurrentRequests = 0;
            }
        }

        private ServiceState GetOrCreateState(string serviceName)
        {
            return _serviceStates.GetOrAdd(serviceName, _ => new ServiceState());
        }
    }
}