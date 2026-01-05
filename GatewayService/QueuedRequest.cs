namespace GatewayService
{
    public class QueuedRequest
    {
        public Guid RequestId { get; set; } = Guid.NewGuid();
        public string ServiceName { get; set; }
        public Func<CancellationToken, Task> RequestAction { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int RetryCount { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    }
}
