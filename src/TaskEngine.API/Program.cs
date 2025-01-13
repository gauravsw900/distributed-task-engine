using Serilog;
using StackExchange.Redis;
using TaskEngine.Core.Services;
using TaskEngine.API.Hubs;
using TaskEngine.API.BackgroundServices;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddScoped<ITaskQueueService, RedisTaskQueueService>();
builder.Services.AddScoped<IWorkerRegistryService, RedisWorkerRegistryService>();

builder.Services.AddHostedService<TaskMonitorService>();
builder.Services.AddHostedService<StaleWorkerCleanupService>();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(
                builder.Configuration["AllowedOrigins"] ?? "http://localhost:3000"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Distributed Task Engine API",
        Version = "v1"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Engine API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("FrontendPolicy");
app.UseRouting();
app.MapControllers();
app.MapHub<TaskEventsHub>("/hubs/tasks");

app.Run();
