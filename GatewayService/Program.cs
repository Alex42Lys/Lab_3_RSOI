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

builder.Services.AddSingleton<ServiceCircuitBreaker>(sp =>
    new ServiceCircuitBreaker(5, 10));
builder.Services.AddHostedService<RequestQueueService>();
builder.Services.AddSingleton<IRequestQueueService>(sp =>
    sp.GetServices<IHostedService>().OfType<RequestQueueService>().First());
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
