using GatewayService;
using System.Collections.Concurrent;
using System.Threading.Channels;

public interface IRequestQueueService
{
    ValueTask QueueRequestAsync(string serviceName, Func<CancellationToken, Task> requestAction);
    Task CompleteAllPendingRequestsAsync(CancellationToken cancellationToken = default);
}

public class RequestQueueService : BackgroundService, IRequestQueueService
{
    private readonly Channel<QueuedRequest> _queue;
    private readonly ILogger<RequestQueueService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, QueuedRequest> _pendingRequests = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RequestQueueService(ILogger<RequestQueueService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Создаем неограниченный канал
        _queue = Channel.CreateUnbounded<QueuedRequest>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
    }

    public ValueTask QueueRequestAsync(string serviceName, Func<CancellationToken, Task> requestAction)
    {
        var request = new QueuedRequest
        {
            ServiceName = serviceName,
            RequestAction = requestAction
        };

        _pendingRequests[request.RequestId] = request;

        return _queue.Writer.WriteAsync(request);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Request Queue Service is starting");

        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessRequestAsync(request, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued request for service: {ServiceName}",
                    request.ServiceName);
            }
        }
    }

    private async Task ProcessRequestAsync(QueuedRequest request, CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(request.Timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await request.RequestAction(cts.Token);

                _pendingRequests.TryRemove(request.RequestId, out _);
                _logger.LogInformation(
                    "Successfully processed queued request for service: {ServiceName}",
                    request.ServiceName);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                request.RetryCount++;

                _logger.LogWarning(ex,
                    "Attempt {RetryCount} failed for service {ServiceName}. Retrying in 2 seconds...",
                    request.RetryCount, request.ServiceName);

                // Ждем перед повторной попыткой
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
            }
        }

        _logger.LogError("Request for service {ServiceName} timed out after {Timeout}",
            request.ServiceName, request.Timeout);
        _pendingRequests.TryRemove(request.RequestId, out _);
    }

    public async Task CompleteAllPendingRequestsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            while (!_pendingRequests.IsEmpty && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request Queue Service is stopping");
        await CompleteAllPendingRequestsAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}