namespace TaskEngine.Shared;

public static class RedisKeys
{
    public const string TaskQueueCritical = "task:queue:critical";
    public const string TaskQueueHigh = "task:queue:high";
    public const string TaskQueueNormal = "task:queue:normal";
    public const string TaskQueueLow = "task:queue:low";

    public static string TaskDetails(string taskId) => $"task:details:{taskId}";
    public static string TaskResult(string taskId) => $"task:result:{taskId}";
    public static string WorkerInfo(string workerId) => $"worker:info:{workerId}";

    public const string WorkerSet = "workers:active";
    public const string ProcessingSet = "task:processing";
    public const string StatsKey = "stats:global";
    public const string TaskChannel = "task:events";
    public const string WorkerHeartbeatChannel = "worker:heartbeat";
}

public static class QueueNames
{
    public static string ForPriority(Models.TaskPriority priority) => priority switch
    {
        Models.TaskPriority.Critical => RedisKeys.TaskQueueCritical,
        Models.TaskPriority.High => RedisKeys.TaskQueueHigh,
        Models.TaskPriority.Normal => RedisKeys.TaskQueueNormal,
        Models.TaskPriority.Low => RedisKeys.TaskQueueLow,
        _ => RedisKeys.TaskQueueNormal
    };

    public static string[] AllQueues =>
    [
        RedisKeys.TaskQueueCritical,
        RedisKeys.TaskQueueHigh,
        RedisKeys.TaskQueueNormal,
        RedisKeys.TaskQueueLow
    ];
}
