using TaskEngine.Shared.Models;

namespace TaskEngine.Worker.Handlers;

public interface ITaskHandler
{
    string TaskType { get; }
    Task<TaskResult> ExecuteAsync(TaskItem task, CancellationToken cancellationToken);
}

public class EmailTaskHandler : ITaskHandler
{
    private readonly ILogger<EmailTaskHandler> _logger;
    public string TaskType => "email";

    public EmailTaskHandler(ILogger<EmailTaskHandler> logger) => _logger = logger;

    public async Task<TaskResult> ExecuteAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var to = task.Payload.GetValueOrDefault("to", "unknown")?.ToString();
        var subject = task.Payload.GetValueOrDefault("subject", "No Subject")?.ToString();

        _logger.LogInformation("Sending email to {To}: {Subject}", to, subject);

        // TODO: wire up actual SMTP client
        await Task.Delay(Random.Shared.Next(200, 800), cancellationToken);

        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Output = $"Email sent to {to} with subject '{subject}'"
        };
    }
}

public class DataProcessingTaskHandler : ITaskHandler
{
    private readonly ILogger<DataProcessingTaskHandler> _logger;
    public string TaskType => "data-processing";

    public DataProcessingTaskHandler(ILogger<DataProcessingTaskHandler> logger) => _logger = logger;

    public async Task<TaskResult> ExecuteAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var recordCount = task.Payload.GetValueOrDefault("record_count", 1000);
        _logger.LogInformation("Processing {Records} records for task {TaskId}", recordCount, task.Id);

        var stages = new[] { "Extract", "Transform", "Validate", "Load" };
        foreach (var stage in stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[{TaskId}] Stage: {Stage}", task.Id, stage);
            await Task.Delay(Random.Shared.Next(300, 1200), cancellationToken);
        }

        var processed = (int)(Convert.ToDouble(recordCount) * (0.95 + Random.Shared.NextDouble() * 0.05));
        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Output = $"Processed {processed} records across {stages.Length} stages"
        };
    }
}

public class ReportGenerationTaskHandler : ITaskHandler
{
    private readonly ILogger<ReportGenerationTaskHandler> _logger;
    public string TaskType => "report-generation";

    public ReportGenerationTaskHandler(ILogger<ReportGenerationTaskHandler> logger) => _logger = logger;

    public async Task<TaskResult> ExecuteAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var reportType = task.Payload.GetValueOrDefault("report_type", "pdf")?.ToString();
        var dateRange = task.Payload.GetValueOrDefault("date_range", "last-30-days")?.ToString();

        _logger.LogInformation("Generating {ReportType} report for {DateRange}", reportType, dateRange);

        await Task.Delay(Random.Shared.Next(1000, 3000), cancellationToken);

        var fileName = $"report_{task.Id[..8]}_{DateTime.UtcNow:yyyyMMdd}.{reportType}";
        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Output = $"Report generated: {fileName} (date range: {dateRange})"
        };
    }
}

public class WebhookTaskHandler : ITaskHandler
{
    private readonly ILogger<WebhookTaskHandler> _logger;
    public string TaskType => "webhook";

    public WebhookTaskHandler(ILogger<WebhookTaskHandler> logger) => _logger = logger;

    public async Task<TaskResult> ExecuteAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var url = task.Payload.GetValueOrDefault("url", "")?.ToString();
        var eventType = task.Payload.GetValueOrDefault("event_type", "generic")?.ToString();

        if (string.IsNullOrEmpty(url))
            return new TaskResult { TaskId = task.Id, Success = false, ErrorMessage = "Webhook URL is required" };

        _logger.LogInformation("Sending webhook [{EventType}] to {Url}", eventType, url);

        // TODO: replace with actual HttpClient + Polly retry
        await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);

        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Output = $"Webhook delivered to {url} (event: {eventType})"
        };
    }
}

public class ImageResizeTaskHandler : ITaskHandler
{
    private readonly ILogger<ImageResizeTaskHandler> _logger;
    public string TaskType => "image-resize";

    public ImageResizeTaskHandler(ILogger<ImageResizeTaskHandler> logger) => _logger = logger;

    public async Task<TaskResult> ExecuteAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var width = task.Payload.GetValueOrDefault("width", 800);
        var height = task.Payload.GetValueOrDefault("height", 600);
        var format = task.Payload.GetValueOrDefault("format", "webp")?.ToString();

        _logger.LogInformation("Resizing image to {W}x{H} ({Format})", width, height, format);

        await Task.Delay(Random.Shared.Next(500, 2000), cancellationToken);

        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Output = $"Image resized to {width}x{height} and saved as {format}"
        };
    }
}
