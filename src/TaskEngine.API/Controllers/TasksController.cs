using Microsoft.AspNetCore.Mvc;
using TaskEngine.Core.Services;
using TaskEngine.Shared.Models;

namespace TaskEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly ITaskQueueService _queueService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskQueueService queueService, ILogger<TasksController> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubmitTaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitTask([FromBody] SubmitTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
            return BadRequest("Task type is required");

        var task = new TaskItem
        {
            Name = request.Name ?? request.Type,
            Type = request.Type,
            Priority = request.Priority,
            Payload = request.Payload ?? new(),
            MaxRetries = request.MaxRetries,
            TimeoutSeconds = request.TimeoutSeconds,
            Tags = request.Tags ?? new()
        };

        var taskId = await _queueService.EnqueueAsync(task);
        _logger.LogInformation("Task {TaskId} submitted via API", taskId);

        return CreatedAtAction(nameof(GetTask), new { taskId }, new SubmitTaskResponse
        {
            TaskId = taskId,
            Status = "Queued",
            Message = "Task successfully enqueued"
        });
    }

    [HttpGet("{taskId}")]
    [ProducesResponseType(typeof(TaskItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTask(string taskId)
    {
        var task = await _queueService.GetTaskAsync(taskId);
        return task == null ? NotFound($"Task {taskId} not found") : Ok(task);
    }

    [HttpDelete("{taskId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelTask(string taskId)
    {
        var task = await _queueService.GetTaskAsync(taskId);
        if (task == null) return NotFound();

        await _queueService.CancelTaskAsync(taskId);
        return NoContent();
    }

    [HttpPost("{taskId}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryTask(string taskId)
    {
        var task = await _queueService.GetTaskAsync(taskId);
        if (task == null) return NotFound();

        if (task.Status != Shared.Models.TaskStatus.Failed)
            return BadRequest("Only failed tasks can be retried");

        await _queueService.RequeueFailedTaskAsync(taskId);
        return Ok(new { message = "Task requeued for retry" });
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(QueueStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _queueService.GetQueueStatsAsync();
        return Ok(stats);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTasks([FromQuery] int count = 50)
    {
        var tasks = await _queueService.GetRecentTasksAsync(Math.Min(count, 200));
        return Ok(tasks);
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkSubmitResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> BulkSubmitTasks([FromBody] List<SubmitTaskRequest> requests)
    {
        if (requests.Count > 100)
            return BadRequest("Maximum 100 tasks per bulk request");

        var taskIds = new List<string>();
        foreach (var request in requests)
        {
            var task = new TaskItem
            {
                Name = request.Name ?? request.Type,
                Type = request.Type,
                Priority = request.Priority,
                Payload = request.Payload ?? new(),
                MaxRetries = request.MaxRetries,
                TimeoutSeconds = request.TimeoutSeconds,
                Tags = request.Tags ?? new()
            };
            taskIds.Add(await _queueService.EnqueueAsync(task));
        }

        return StatusCode(201, new BulkSubmitResponse { TaskIds = taskIds, Count = taskIds.Count });
    }
}

public class SubmitTaskRequest
{
    public string? Name { get; set; }
    public string Type { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public Dictionary<string, object>? Payload { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
    public List<string>? Tags { get; set; }
}

public class SubmitTaskResponse
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class BulkSubmitResponse
{
    public List<string> TaskIds { get; set; } = new();
    public int Count { get; set; }
}
