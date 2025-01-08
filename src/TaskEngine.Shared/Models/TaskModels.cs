namespace TaskEngine.Shared.Models;

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public TaskStatus Status { get; set; } = TaskStatus.Queued;
    public Dictionary<string, object> Payload { get; set; } = new();
    public int MaxRetries { get; set; } = 3;
    public int RetryCount { get; set; } = 0;
    public int TimeoutSeconds { get; set; } = 300;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssignedWorker { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Result { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class TaskResult
{
    public string TaskId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
}

public class WorkerInfo
{
    public string WorkerId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public WorkerStatus Status { get; set; } = WorkerStatus.Idle;
    public int TasksProcessed { get; set; }
    public int TasksFailed { get; set; }
    public string? CurrentTaskId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public List<string> SupportedTaskTypes { get; set; } = new();
    public double CpuUsage { get; set; }
    public double MemoryUsageMb { get; set; }
}

public class QueueStats
{
    public long PendingTasks { get; set; }
    public long ProcessingTasks { get; set; }
    public long CompletedTasks { get; set; }
    public long FailedTasks { get; set; }
    public int ActiveWorkers { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public double ThroughputPerMinute { get; set; }
    public Dictionary<string, long> TasksByPriority { get; set; } = new();
    public Dictionary<string, long> TasksByType { get; set; } = new();
}

public enum TaskPriority
{
    Low = 1,
    Normal = 5,
    High = 8,
    Critical = 10
}

public enum TaskStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled,
    TimedOut
}

public enum WorkerStatus
{
    Idle,
    Busy,
    Draining,
    Offline
}
