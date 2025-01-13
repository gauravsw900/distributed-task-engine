using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using TaskEngine.API.Hubs;
using TaskEngine.Core.Services;
using TaskEngine.Shared;
using Newtonsoft.Json;

namespace TaskEngine.API.BackgroundServices;

public class TaskMonitorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHubContext<TaskEventsHub> _hub;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TaskMonitorService> _logger;

    public TaskMonitorService(
        IServiceProvider services,
        IHubContext<TaskEventsHub> hub,
        IConnectionMultiplexer redis,
        ILogger<TaskMonitorService> logger)
    {
        _services = services;
        _hub = hub;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = _redis.GetSubscriber();
        await sub.SubscribeAsync(RedisChannel.Literal(RedisKeys.TaskChannel), async (channel, message) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<dynamic>(message!);
                await _hub.Clients.All.SendAsync("TaskEvent", (object)payload!, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to forward task event");
            }
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<ITaskQueueService>();
                var stats = await queueService.GetQueueStatsAsync();
                await _hub.Clients.All.SendAsync("StatsUpdate", stats, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast stats");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}

public class StaleWorkerCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<StaleWorkerCleanupService> _logger;

    public StaleWorkerCleanupService(IServiceProvider services, ILogger<StaleWorkerCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<IWorkerRegistryService>();
                await registry.PruneStaleWorkersAsync(staleThresholdSeconds: 30);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to prune stale workers");
            }

            await Task.Delay(15000, stoppingToken);
        }
    }
}
