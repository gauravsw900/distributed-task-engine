using TaskEngine.Shared;
using TaskEngine.Shared.Models;
using StackExchange.Redis;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace TaskEngine.Core.Services;

public interface ITaskQueueService
{
    Task<string> EnqueueAsync(TaskItem task);
    Task<TaskItem?> DequeueAsync(string workerId, IEnumerable<string>? supportedTypes = null, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetTaskAsync(string taskId);
    Task UpdateTaskStatusAsync(string taskId, Shared.Models.TaskStatus status, string? workerId = null, string? result = null, string? errorMessage = null);
    Task CompleteTaskAsync(TaskResult taskResult);
    Task RequeueFailedTaskAsync(string taskId);
    Task<QueueStats> GetQueueStatsAsync();
    Task<IEnumerable<TaskItem>> GetRecentTasksAsync(int count = 50);
    Task CancelTaskAsync(string taskId);
}

public class RedisTaskQueueService : ITaskQueueService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTaskQueueService> _logger;

    public RedisTaskQueueService(IConnectionMultiplexer redis, ILogger<RedisTaskQueueService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> EnqueueAsync(TaskItem task)
    {
        var db = _redis.GetDatabase();
        var queueKey = QueueNames.ForPriority(task.Priority);

        task.Status = Shared.Models.TaskStatus.Queued;
        task.CreatedAt = DateTime.UtcNow;

        var serialized = JsonConvert.SerializeObject(task);

        await db.StringSetAsync(
            RedisKeys.TaskDetails(task.Id),
            serialized,
            TimeSpan.FromHours(24)
        );

        // score = timestamp so items at same priority are processed FIFO
        var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await db.SortedSetAddAsync(queueKey, task.Id, score);

        await PublishEventAsync("task.enqueued", task.Id, task.Type, task.Priority.ToString());
        await db.HashIncrementAsync(RedisKeys.StatsKey, "total_enqueued");

        _logger.LogInformation("Task {TaskId} ({Type}) enqueued to {Queue}", task.Id, task.Type, queueKey);
        return task.Id;
    }

    public async Task<TaskItem?> DequeueAsync(string workerId, IEnumerable<string>? supportedTypes = null, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();

        foreach (var queue in QueueNames.AllQueues)
        {
            var entries = await db.SortedSetRangeByScoreWithScoresAsync(queue, take: 10);
            if (entries.Length == 0) continue;

            foreach (var entry in entries)
            {
                var taskId = entry.Element.ToString();
                var taskJson = await db.StringGetAsync(RedisKeys.TaskDetails(taskId));
                if (taskJson.IsNullOrEmpty)
                {
                    await db.SortedSetRemoveAsync(queue, taskId);
                    continue;
                }

                var task = JsonConvert.DeserializeObject<TaskItem>(taskJson!);
                if (task == null) continue;

                if (supportedTypes != null && supportedTypes.Any() && !supportedTypes.Contains(task.Type))
                    continue;

                // atomic remove - if another worker grabbed it first this returns false
                var removed = await db.SortedSetRemoveAsync(queue, taskId);
                if (!removed) continue;

                await UpdateTaskStatusAsync(taskId, Shared.Models.TaskStatus.Processing, workerId);
                await db.SetAddAsync(RedisKeys.ProcessingSet, taskId);

                _logger.LogInformation("Worker {WorkerId} dequeued task {TaskId}", workerId, taskId);
                return task;
            }
        }

        return null;
    }

    public async Task<TaskItem?> GetTaskAsync(string taskId)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(RedisKeys.TaskDetails(taskId));
        return json.IsNullOrEmpty ? null : JsonConvert.DeserializeObject<TaskItem>(json!);
    }

    public async Task UpdateTaskStatusAsync(string taskId, Shared.Models.TaskStatus status, string? workerId = null, string? result = null, string? errorMessage = null)
    {
        var db = _redis.GetDatabase();
        var task = await GetTaskAsync(taskId);
        if (task == null) return;

        task.Status = status;
        if (workerId != null) task.AssignedWorker = workerId;
        if (result != null) task.Result = result;
        if (errorMessage != null) task.ErrorMessage = errorMessage;

        if (status == Shared.Models.TaskStatus.Processing)
            task.StartedAt = DateTime.UtcNow;
        else if (status is Shared.Models.TaskStatus.Completed or Shared.Models.TaskStatus.Failed or Shared.Models.TaskStatus.TimedOut)
            task.CompletedAt = DateTime.UtcNow;

        var serialized = JsonConvert.SerializeObject(task);
        await db.StringSetAsync(RedisKeys.TaskDetails(taskId), serialized, TimeSpan.FromHours(24));
        await PublishEventAsync($"task.{status.ToString().ToLower()}", taskId, task.Type, status.ToString());
    }

    public async Task CompleteTaskAsync(TaskResult taskResult)
    {
        var db = _redis.GetDatabase();
        var status = taskResult.Success ? Shared.Models.TaskStatus.Completed : Shared.Models.TaskStatus.Failed;

        await UpdateTaskStatusAsync(taskResult.TaskId, status, result: taskResult.Output, errorMessage: taskResult.ErrorMessage);
        await db.SetRemoveAsync(RedisKeys.ProcessingSet, taskResult.TaskId);

        var resultJson = JsonConvert.SerializeObject(taskResult);
        await db.StringSetAsync(RedisKeys.TaskResult(taskResult.TaskId), resultJson, TimeSpan.FromHours(2));

        var statField = taskResult.Success ? "total_completed" : "total_failed";
        await db.HashIncrementAsync(RedisKeys.StatsKey, statField);
    }

    public async Task RequeueFailedTaskAsync(string taskId)
    {
        var task = await GetTaskAsync(taskId);
        if (task == null) return;

        if (task.RetryCount >= task.MaxRetries)
        {
            _logger.LogWarning("Task {TaskId} exceeded max retries ({Max})", taskId, task.MaxRetries);
            await UpdateTaskStatusAsync(taskId, Shared.Models.TaskStatus.Failed, errorMessage: "Max retries exceeded");
            return;
        }

        task.RetryCount++;
        task.Status = Shared.Models.TaskStatus.Queued;
        task.AssignedWorker = null;
        task.ErrorMessage = null;

        var db = _redis.GetDatabase();
        await db.StringSetAsync(RedisKeys.TaskDetails(taskId), JsonConvert.SerializeObject(task), TimeSpan.FromHours(24));

        // penalise score slightly so retries don't jump the queue
        var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (task.RetryCount * 5000);
        await db.SortedSetAddAsync(QueueNames.ForPriority(task.Priority), taskId, score);

        _logger.LogInformation("Task {TaskId} requeued (retry {Retry}/{Max})", taskId, task.RetryCount, task.MaxRetries);
    }

    public async Task<QueueStats> GetQueueStatsAsync()
    {
        var db = _redis.GetDatabase();
        var stats = new QueueStats();

        stats.PendingTasks = 0;
        foreach (var queue in QueueNames.AllQueues)
            stats.PendingTasks += await db.SortedSetLengthAsync(queue);

        stats.ProcessingTasks = await db.SetLengthAsync(RedisKeys.ProcessingSet);

        var globalStats = await db.HashGetAllAsync(RedisKeys.StatsKey);
        var statsDict = globalStats.ToDictionary(e => e.Name.ToString(), e => (long)e.Value);
        stats.CompletedTasks = statsDict.GetValueOrDefault("total_completed");
        stats.FailedTasks = statsDict.GetValueOrDefault("total_failed");

        var workerKeys = _redis.GetServer(_redis.GetEndPoints().First()).Keys(pattern: "worker:info:*");
        stats.ActiveWorkers = workerKeys.Count();

        stats.TasksByPriority = new Dictionary<string, long>
        {
            ["Critical"] = await db.SortedSetLengthAsync(RedisKeys.TaskQueueCritical),
            ["High"] = await db.SortedSetLengthAsync(RedisKeys.TaskQueueHigh),
            ["Normal"] = await db.SortedSetLengthAsync(RedisKeys.TaskQueueNormal),
            ["Low"] = await db.SortedSetLengthAsync(RedisKeys.TaskQueueLow)
        };

        return stats;
    }

    public async Task<IEnumerable<TaskItem>> GetRecentTasksAsync(int count = 50)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: "task:details:*").Take(count);
        var db = _redis.GetDatabase();
        var tasks = new List<TaskItem>();

        foreach (var key in keys)
        {
            var json = await db.StringGetAsync(key);
            if (!json.IsNullOrEmpty)
            {
                var task = JsonConvert.DeserializeObject<TaskItem>(json!);
                if (task != null) tasks.Add(task);
            }
        }

        return tasks.OrderByDescending(t => t.CreatedAt);
    }

    public async Task CancelTaskAsync(string taskId)
    {
        var db = _redis.GetDatabase();
        foreach (var queue in QueueNames.AllQueues)
            await db.SortedSetRemoveAsync(queue, taskId);

        await UpdateTaskStatusAsync(taskId, Shared.Models.TaskStatus.Cancelled);
        await db.SetRemoveAsync(RedisKeys.ProcessingSet, taskId);
    }

    private async Task PublishEventAsync(string eventType, string taskId, string taskType, string data)
    {
        try
        {
            var sub = _redis.GetSubscriber();
            var payload = JsonConvert.SerializeObject(new { eventType, taskId, taskType, data, timestamp = DateTime.UtcNow });
            await sub.PublishAsync(RedisChannel.Literal(RedisKeys.TaskChannel), payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish event {EventType}", eventType);
        }
    }
}
