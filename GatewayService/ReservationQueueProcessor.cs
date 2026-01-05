using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GatewayService.Services
{
    public class ReservationQueueProcessor : BackgroundService
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly HttpClient _httpClient;
        private readonly ServiceCircuitBreaker _circuitBreaker;
        private readonly ILogger<ReservationQueueProcessor> _logger;
        private readonly IConfiguration _configuration;

        public ReservationQueueProcessor(
            RabbitMQService rabbitMQService,
            IHttpClientFactory httpClientFactory,
            ServiceCircuitBreaker circuitBreaker,
            ILogger<ReservationQueueProcessor> logger,
            IConfiguration configuration)
        {
            _rabbitMQService = rabbitMQService;
            _httpClient = httpClientFactory.CreateClient("Default");
            _circuitBreaker = circuitBreaker;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReservationQueueProcessor запущен");

            // Ждем немного перед запуском потребителя
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            // Запускаем потребителя очереди
            _rabbitMQService.StartConsuming<dynamic>(ProcessMessageAsync);

            // Держим сервис запущенным
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                // Периодически логируем статистику
                var messageCount = _rabbitMQService.GetMessageCount();
                if (messageCount > 0)
                {
                    _logger.LogInformation("Сообщений в очереди ожидания: {Count}", messageCount);
                }
            }
        }

        private async Task<bool> ProcessMessageAsync(dynamic message)
        {
            try
            {

                string messageType = message.Type.ToString();

                switch (messageType)
                {
                    case "TakeBook":
                        return await ProcessTakeBookMessageAsync(message);

                    case "ReturnBook":
                        return await ProcessReturnBookMessageAsync(message);

                    case "UpdateRating":
                        return await ProcessUpdateRatingMessageAsync(message);

                    case "UpdateBookCondition":
                        return await ProcessUpdateBookConditionMessageAsync(message);

                    case "DelayedRatingUpdate":
                        return await ProcessDelayedRatingUpdateMessageAsync(message);

                    case "GetReservationDetails":
                        return await ProcessGetReservationDetailsMessageAsync(message);

                    case "Compensation":
                        return await ProcessCompensationMessageAsync(message);

                    default:
                        _logger.LogWarning("Неизвестный тип сообщения: {Type}", messageType);
                        return true; // Подтверждаем обработку, чтобы удалить из очереди
                }
            }
            catch (Exception ex)
            {

                // Увеличиваем счетчик попыток
                int retryCount = message.RetryCount ?? 0;
                if (retryCount < 3) // Максимум 3 попытки
                {
                    message.RetryCount = retryCount + 1;
                    message.LastError = ex.Message;
                    message.LastRetry = DateTime.UtcNow;

                    // Возвращаем false, чтобы сообщение вернулось в очередь
                    return false;
                }
                else
                {
                    return true; // Удаляем из очереди после 3 неудачных попыток
                }
            }
        }

        private async Task<bool> ProcessTakeBookMessageAsync(dynamic message)
        {

            // Проверяем доступность сервисов
            if (_circuitBreaker.HasTimeOutPassed("ReservationService") &&
                _circuitBreaker.HasTimeOutPassed("RatingService") &&
                _circuitBreaker.HasTimeOutPassed("LibraryService"))
            {
                try
                {
                    // Здесь можно реализовать повторную попытку выполнения TakeBook
                    // или вызвать соответствующий сервис напрямую

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("Сервисы все еще недоступны для обработки TakeBook");
                return false; // Возвращаем в очередь
            }
        }

        private async Task<bool> ProcessReturnBookMessageAsync(dynamic message)
        {
            // Аналогичная логика для ReturnBook
            return true;
        }

        private async Task<bool> ProcessUpdateRatingMessageAsync(dynamic message)
        {
            try
            {
                var url = $"http://rating:8080/Rating/changeRating?delta={message.DeltaRating}";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-User-Name", message.UserName.ToString());

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {

                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<bool> ProcessUpdateBookConditionMessageAsync(dynamic message)
        {
            try
            {
                var url = $"http://library:8080/Library/changeCondition?bookId={message.BookUid}&condition={message.Condition}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<bool> ProcessDelayedRatingUpdateMessageAsync(dynamic message)
        {
            // Логика отложенного обновления рейтинга
            return true;
        }

        private async Task<bool> ProcessGetReservationDetailsMessageAsync(dynamic message)
        {
            // Логика получения деталей резервации
            return true;
        }

        private async Task<bool> ProcessCompensationMessageAsync(dynamic message)
        {
            // Логика компенсирующих операций
            return true;
        }
    }
}