using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace GatewayService.Services
{
    public class RabbitMQService : IDisposable
    {
        private readonly IConnection _connection;
        private readonly dynamic _channel;
        private readonly string _queueName;

        public RabbitMQService(IConfiguration configuration)
        {
            var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
            var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
            var userName = configuration["RabbitMQ:UserName"] ?? "guest";
            var password = configuration["RabbitMQ:Password"] ?? "guest";
            _queueName = configuration["RabbitMQ:QueueName"] ?? "reservation_queue";

            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = hostName,
                    Port = port,
                    UserName = userName,
                    Password = password
                };

                _connection = factory.CreateConnectionAsync().Result;
                _channel = _connection.CreateChannelAsync();

                // Объявляем очередь
                _channel.QueueDeclare(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                Console.WriteLine($"RabbitMQService инициализирован. Очередь: {_queueName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при инициализации RabbitMQService: {ex.Message}");
                throw;
            }
        }

        // Метод для отправки сообщения в очередь
        public void SendMessage<T>(T message)
        {
            try
            {
                var jsonMessage = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                _channel.BasicPublish(
                    exchange: "",
                    routingKey: _queueName,
                    basicProperties: null,
                    body: body);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
                throw;
            }
        }

        // Метод для получения количества сообщений в очереди
        public uint GetMessageCount()
        {
            try
            {
                var queueInfo = _channel.QueueDeclarePassive(_queueName);
                return queueInfo.MessageCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении количества сообщений: {ex.Message}");
                return 0;
            }
        }

        // Метод для запуска потребителя (консьюмера)
        public void StartConsuming<T>(Func<T, Task<bool>> messageHandler)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var messageObject = JsonSerializer.Deserialize<T>(message);

                    if (messageObject != null)
                    {
                        var success = await messageHandler(messageObject);

                        if (success)
                        {
                            _channel.BasicAck(ea.DeliveryTag, multiple: false);
                            Console.WriteLine($"Сообщение обработано успешно");
                        }
                        else
                        {
                            _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                            Console.WriteLine($"Обработка сообщения не удалась. Возврат в очередь.");
                        }
                    }
                    else
                    {
                        _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                        Console.WriteLine($"Не удалось десериализовать сообщение");
                    }
                }
                catch (Exception ex)
                {
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                    Console.WriteLine($"Ошибка при обработке сообщения: {ex.Message}");
                }
            };

            _channel.BasicConsume(
                queue: _queueName,
                autoAck: false,
                consumer: consumer);

            Console.WriteLine($"Запущен потребитель для очереди {_queueName}");
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.CloseAsync();
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при остановке RabbitMQService: {ex.Message}");
            }
        }
    }
}