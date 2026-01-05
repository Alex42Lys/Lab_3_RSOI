using GatewayService;
using GatewayService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

public interface IRequestQueueService
{
}

public class RequestQueueService : BackgroundService, IRequestQueueService
{
    private readonly HttpClient _httpClient;
    private readonly ServiceCircuitBreaker _circuitBreaker;

    public RequestQueueService(HttpClient httpClient, ServiceCircuitBreaker circuitBreaker)
    {
        _httpClient = httpClient;
        _circuitBreaker = circuitBreaker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        Thread.Sleep(10 * 1000);
        var m = _circuitBreaker.queue.FirstOrDefault();
        var url = $"http://rating:8080/Rating/changeRating?delta={m.dlt}";
        var re = new HttpRequestMessage(HttpMethod.Post, url);
        re.Headers.Add("X-User-Name", m.Usr);
        var rrp = await _httpClient.SendAsync(re);
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {

    }
}