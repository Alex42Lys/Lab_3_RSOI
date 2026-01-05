using GatewayService;
using GatewayService.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.Configure<RabbitMQOptions>(
    builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<ServiceCircuitBreaker>(sp =>
    new ServiceCircuitBreaker(5, 10));
builder.Services.AddSingleton<RabbitMQService>();
builder.Services.AddHostedService<RequestQueueService>();
builder.Services.AddSingleton<IRequestQueueService, RequestQueueService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
