using Serilog;
using StackExchange.Redis;
using TaskEngine.Core.Services;
using TaskEngine.Worker.Handlers;
using TaskEngine.Worker.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        var redisConnection = context.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<ITaskQueueService, RedisTaskQueueService>();
        services.AddScoped<IWorkerRegistryService, RedisWorkerRegistryService>();

        services.AddScoped<ITaskHandler, EmailTaskHandler>();
        services.AddScoped<ITaskHandler, DataProcessingTaskHandler>();
        services.AddScoped<ITaskHandler, ReportGenerationTaskHandler>();
        services.AddScoped<ITaskHandler, WebhookTaskHandler>();
        services.AddScoped<ITaskHandler, ImageResizeTaskHandler>();

        services.AddHostedService<WorkerService>();
    })
    .Build();

await host.RunAsync();
