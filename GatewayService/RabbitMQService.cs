using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace GatewayService.Services
{
    public class RabbitMQService : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly string _queueName;
        private readonly ConnectionFactory _factory;
        private IConnection _connection;
        private dynamic _channel;
        private bool _isInitialized = false;

        public RabbitMQService(
            IConfiguration configuration,
            ILogger<RabbitMQService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _queueName = configuration["RabbitMQ:QueueName"] ?? "reservation_queue";

            var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
            var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
            var userName = configuration["RabbitMQ:UserName"] ?? "guest";
            var password = configuration["RabbitMQ:Password"] ?? "guest";

            _factory = new ConnectionFactory()
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _logger.LogInformation("RabbitMQService создан для очереди: {QueueName}", _queueName);
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            lock (this)
            {
                if (_isInitialized) return;

                try
                {
                    _connection = _factory.CreateConnectionAsync().Result;
                    _channel = _connection.CreateChannelAsync();

                    // Объявляем очередь
                    _channel.QueueDeclare(
                        queue: _queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    _isInitialized = true;
                    _logger.LogInformation("RabbitMQService инициализирован");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при инициализации RabbitMQService");
                    _isInitialized = false;
                }
            }
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        // Метод для отправки сообщения в очередь
        public bool SendMessage<T>(T message)
        {
            try
            {
                EnsureInitialized();

                if (!_isInitialized || _channel == null)
                {
                    _logger.LogWarning("RabbitMQ не инициализирован, сообщение не отправлено");
                    return false;
                }

                var jsonMessage = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;

                _channel.BasicPublish(
                    exchange: "",
                    routingKey: _queueName,
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug("Сообщение отправлено в очередь {QueueName}", _queueName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке сообщения в RabbitMQ");
                _isInitialized = false; // Сбрасываем флаг для повторной инициализации
                return false;
            }
        }

        // Метод для отправки сообщения с созданием нового подключения
        public bool SendMessageWithNewConnection<T>(T message)
        {
            try
            {
                using var connection = _factory.CreateConnectionAsync().Result;
                using var channel = connection.CreateChannelAsync().Result;

                channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var jsonMessage = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: _queueName,
                    body: body);

                _logger.LogDebug("Сообщение отправлено в очередь {QueueName} (новое подключение)", _queueName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке сообщения в RabbitMQ (новое подключение)");
                return false;
            }
        }

        // Метод для получения количества сообщений в очереди
        public uint GetMessageCount()
        {
            try
            {
                EnsureInitialized();

                if (!_isInitialized || _channel == null)
                    return 0;

                var queueInfo = _channel.QueueDeclarePassive(_queueName);
                return queueInfo.MessageCount;
            }
            catch
            {
                return 0;
            }
        }

        // Метод для подписки на получение сообщений
        public void StartConsuming<T>(Func<T, Task<bool>> messageHandler)
        {
            try
            {
                EnsureInitialized();

                if (!_isInitialized || _channel == null)
                {
                    _logger.LogError("Не удалось запустить потребителя: RabbitMQ не инициализирован");
                    return;
                }

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
                                _logger.LogDebug("Сообщение обработано успешно");
                            }
                            else
                            {
                                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                                _logger.LogWarning("Обработка сообщения не удалась, возвращаем в очередь");
                            }
                        }
                        else
                        {
                            _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                            _logger.LogWarning("Не удалось десериализовать сообщение, отбрасываем");
                        }
                    }
                    catch (Exception ex)
                    {
                        _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                        _logger.LogError(ex, "Ошибка при обработке сообщения из очереди");
                    }
                };

                _channel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Запущен потребитель для очереди {QueueName}", _queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запуске потребителя RabbitMQ");
                _isInitialized = false;
            }
        }

        // Простой метод для получения одного сообщения
        public T GetMessage<T>()
        {
            try
            {
                EnsureInitialized();

                if (!_isInitialized || _channel == null)
                    return default;

                var result = _channel.BasicGet(_queueName, autoAck: true);
                if (result == null)
                    return default;

                var body = result.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                return JsonSerializer.Deserialize<T>(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сообщения из очереди");
                return default;
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.CloseAsync();
                _channel?.Dispose();
                _connection?.Dispose();
                _logger.LogInformation("RabbitMQService остановлен");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при остановке RabbitMQService");
            }
        }
    }
}