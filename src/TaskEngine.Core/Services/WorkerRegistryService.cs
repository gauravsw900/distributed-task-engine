using TaskEngine.Shared;
using TaskEngine.Shared.Models;
using StackExchange.Redis;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace TaskEngine.Core.Services;

public interface IWorkerRegistryService
{
    Task RegisterWorkerAsync(WorkerInfo worker);
    Task UpdateHeartbeatAsync(string workerId, WorkerStatus status, string? currentTaskId = null, double cpuUsage = 0, double memoryMb = 0);
    Task UnregisterWorkerAsync(string workerId);
    Task<IEnumerable<WorkerInfo>> GetActiveWorkersAsync();
    Task<WorkerInfo?> GetWorkerAsync(string workerId);
    Task PruneStaleWorkersAsync(int staleThresholdSeconds = 30);
}

public class RedisWorkerRegistryService : IWorkerRegistryService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisWorkerRegistryService> _logger;

    public RedisWorkerRegistryService(IConnectionMultiplexer redis, ILogger<RedisWorkerRegistryService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task RegisterWorkerAsync(WorkerInfo worker)
    {
        var db = _redis.GetDatabase();
        worker.RegisteredAt = DateTime.UtcNow;
        worker.LastHeartbeat = DateTime.UtcNow;

        var json = JsonConvert.SerializeObject(worker);
        await db.StringSetAsync(RedisKeys.WorkerInfo(worker.WorkerId), json, TimeSpan.FromSeconds(60));
        await db.SetAddAsync(RedisKeys.WorkerSet, worker.WorkerId);

        _logger.LogInformation("Worker {WorkerId} registered on {Host}", worker.WorkerId, worker.HostName);
    }

    public async Task UpdateHeartbeatAsync(string workerId, WorkerStatus status, string? currentTaskId = null, double cpuUsage = 0, double memoryMb = 0)
    {
        var db = _redis.GetDatabase();
        var worker = await GetWorkerAsync(workerId);
        if (worker == null) return;

        worker.LastHeartbeat = DateTime.UtcNow;
        worker.Status = status;
        worker.CurrentTaskId = currentTaskId;
        worker.CpuUsage = cpuUsage;
        worker.MemoryUsageMb = memoryMb;

        var json = JsonConvert.SerializeObject(worker);
        await db.StringSetAsync(RedisKeys.WorkerInfo(workerId), json, TimeSpan.FromSeconds(60));
    }

    public async Task UnregisterWorkerAsync(string workerId)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(RedisKeys.WorkerInfo(workerId));
        await db.SetRemoveAsync(RedisKeys.WorkerSet, workerId);
        _logger.LogInformation("Worker {WorkerId} unregistered", workerId);
    }

    public async Task<IEnumerable<WorkerInfo>> GetActiveWorkersAsync()
    {
        var db = _redis.GetDatabase();
        var workerIds = await db.SetMembersAsync(RedisKeys.WorkerSet);
        var workers = new List<WorkerInfo>();

        foreach (var workerId in workerIds)
        {
            var json = await db.StringGetAsync(RedisKeys.WorkerInfo(workerId.ToString()));
            if (!json.IsNullOrEmpty)
            {
                var worker = JsonConvert.DeserializeObject<WorkerInfo>(json!);
                if (worker != null) workers.Add(worker);
            }
        }

        return workers;
    }

    public async Task<WorkerInfo?> GetWorkerAsync(string workerId)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(RedisKeys.WorkerInfo(workerId));
        return json.IsNullOrEmpty ? null : JsonConvert.DeserializeObject<WorkerInfo>(json!);
    }

    public async Task PruneStaleWorkersAsync(int staleThresholdSeconds = 30)
    {
        var workers = await GetActiveWorkersAsync();
        var threshold = DateTime.UtcNow.AddSeconds(-staleThresholdSeconds);

        foreach (var worker in workers.Where(w => w.LastHeartbeat < threshold))
        {
            _logger.LogWarning("Pruning stale worker {WorkerId} (last seen: {LastHeartbeat})", worker.WorkerId, worker.LastHeartbeat);
            await UnregisterWorkerAsync(worker.WorkerId);
        }
    }
}
