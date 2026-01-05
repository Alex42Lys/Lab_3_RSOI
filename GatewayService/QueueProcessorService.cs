using GatewayService;
using GatewayService.Services;

public class QueueProcessorService : BackgroundService
{
    private readonly RabbitMQService _rabbitMQService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<QueueProcessorService> _logger;

    public QueueProcessorService(
        RabbitMQService rabbitMQService,
        IHttpClientFactory httpClientFactory,
        ILogger<QueueProcessorService> logger)
    {
        _rabbitMQService = rabbitMQService;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QueueProcessorService запущен");

        // Обрабатываем очередь каждые 30 секунд
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Получаем сообщения из очереди и обрабатываем их
                await ProcessQueuedMessages(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в QueueProcessorService");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }

    private async Task ProcessQueuedMessages(CancellationToken cancellationToken)
    {
        // Здесь можно реализовать логику получения сообщений из очереди
        // и их обработки. Например, использовать RabbitMQ consumer.

        // Примерная логика:
        // 1. Получить сообщение из очереди
        // 2. Определить тип операции
        // 3. Выполнить соответствующую операцию с повтором при необходимости
        // 4. Удалить сообщение из очереди при успешном выполнении
        // 5. Если после N попыток не удалось - переместить в dead letter queue

        _logger.LogInformation("Проверка очереди сообщений...");
    }

    // Метод для обработки увеличения количества книг
    private async Task<bool> ProcessIncreaseBookCount(dynamic message)
    {
        try
        {
            var url = $"http://library:8080/Library/changeCount?bookId={message.BookUid}&libId={message.LibraryUid}&delta={message.Delta}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Успешно обновлено количество книг для BookUid: {message.BookUid}");
                return true;
            }
            else
            {
                _logger.LogWarning($"Не удалось обновить количество книг для BookUid: {message.BookUid}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при обновлении количества книг для BookUid: {message.BookUid}");
            return false;
        }
    }

    // Метод для обработки обновления рейтинга
    // В классе ReservationQueueProcessor

    private async Task<bool> ProcessUpdateRating(dynamic message)
    {
        try
        {
            var url = $"http://rating:8080/Rating/changeRating?delta={message.DeltaRating}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-User-Name", message.UserName.ToString());

            // Таймаут 10 секунд ТОЛЬКО при повторных попытках
            int retryCount = message.RetryCount ?? 0;
            if (retryCount > 0)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {

                    return true;
                }
            }
            else
            {
                // Первая попытка - без таймаута
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}