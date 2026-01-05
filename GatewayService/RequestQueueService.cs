using GatewayService;
using GatewayService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

public interface IRequestQueueService
{
}

public class RequestQueueService : BackgroundService, IRequestQueueService
{
    private readonly HttpClient _httpClient;
    private readonly ServiceCircuitBreaker _circuitBreaker;
    private readonly ILogger<RequestQueueService> _logger;

    public RequestQueueService(
        HttpClient httpClient,
        ServiceCircuitBreaker circuitBreaker,
        ILogger<RequestQueueService> logger)
    {
        _httpClient = httpClient;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RequestQueueService starting...");

        // Дадим время другим сервисам запуститься
        await Task.Delay(10 * 1000, stoppingToken);

        _logger.LogInformation("Starting to process message queue...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Проверяем, есть ли сообщения в очереди
                if (_circuitBreaker.queue.Count > 0)
                {
                    _logger.LogInformation("Found {Count} messages in queue", _circuitBreaker.queue.Count);

                    // Берем первое сообщение
                    var message = _circuitBreaker.queue.FirstOrDefault();

                    if (message != null)
                    {
                        _logger.LogInformation(
                            "Processing message for user '{User}' with delta {Delta}",
                            message.Usr, message.dlt);

                        // Формируем URL
                        var url = $"http://rating:8080/Rating/changeRating?delta={message.dlt}";
                        _logger.LogDebug("Sending request to: {Url}", url);

                        // Создаем запрос
                        var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Headers.Add("X-User-Name", message.Usr);

                        // Логируем заголовки
                        _logger.LogDebug("Request headers: X-User-Name = {Username}", message.Usr);

                        // Отправляем запрос
                        _logger.LogInformation("Sending rating update request...");
                        var startTime = DateTime.UtcNow;

                        var response = await _httpClient.SendAsync(request, stoppingToken);

                        var duration = DateTime.UtcNow - startTime;
                        _logger.LogInformation("Request completed in {Duration}ms with status: {StatusCode}",
                            duration.TotalMilliseconds, response.StatusCode);

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation(
                                "Successfully updated rating for user '{User}' with delta {Delta}",
                                message.Usr, message.dlt);

                            // Удаляем обработанное сообщение из очереди
                            _circuitBreaker.queue.TryTake(out var _);
                            _logger.LogInformation("Message removed from queue");
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogError(
                                "Failed to update rating. Status: {StatusCode}, Response: {Content}",
                                response.StatusCode, errorContent);

                            // Если ошибка, оставляем сообщение в очереди для повторной попытки
                            _logger.LogWarning("Message kept in queue for retry");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Found null message in queue");
                        _circuitBreaker.queue.TryTake(out var _);
                    }
                }
                else
                {
                    // Если очередь пуста, ждем перед следующей проверкой
                    _logger.LogDebug("Queue is empty, waiting 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Service cancellation requested");
                break;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request failed while processing queue");

                // Ждем перед повторной попыткой
                await Task.Delay(3000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in RequestQueueService");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("RequestQueueService stopping...");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RequestQueueService is shutting down...");

        // Логируем состояние очереди при остановке
        if (_circuitBreaker.queue.Count > 0)
        {
            _logger.LogWarning("Queue has {Count} unprocessed messages on shutdown",
                _circuitBreaker.queue.Count);

            foreach (var message in _circuitBreaker.queue)
            {
                _logger.LogWarning("Unprocessed message - User: {User}, Delta: {Delta}",
                    message.Usr, message.dlt);
            }
        }
        else
        {
            _logger.LogInformation("Queue is empty on shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}