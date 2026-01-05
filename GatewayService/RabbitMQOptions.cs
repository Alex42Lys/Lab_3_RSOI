namespace GatewayService
{
    public class RabbitMQOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string QueueName { get; set; } = "reservation_queue";
        public string RetryQueueName { get; set; } = "reservation_retry_queue";
        public string DeadLetterQueueName { get; set; } = "reservation_dead_letter";
    }
}
