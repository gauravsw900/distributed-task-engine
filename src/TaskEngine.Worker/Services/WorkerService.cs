using TaskEngine.Core.Services;
using TaskEngine.Shared.Models;
using TaskEngine.Worker.Handlers;
using System.Diagnostics;

namespace TaskEngine.Worker.Services;

public class WorkerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WorkerService> _logger;
    private readonly string _workerId;
    private readonly string _hostName;

    public WorkerService(IServiceProvider services, ILogger<WorkerService> logger)
    {
        _services = services;
        _logger = logger;
        _workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid().ToString()[..8]}";
        _hostName = Environment.MachineName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} starting on {Host}", _workerId, _hostName);

        var handlers = _services.GetServices<ITaskHandler>().ToList();
        var supportedTypes = handlers.Select(h => h.TaskType).ToList();

        using var scope = _services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IWorkerRegistryService>();

        await registry.RegisterWorkerAsync(new WorkerInfo
        {
            WorkerId = _workerId,
            HostName = _hostName,
            Status = WorkerStatus.Idle,
            SupportedTaskTypes = supportedTypes
        });

        _ = HeartbeatLoopAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextTaskAsync(supportedTypes, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker loop");
                await Task.Delay(5000, stoppingToken);
            }
        }

        await registry.UnregisterWorkerAsync(_workerId);
        _logger.LogInformation("Worker {WorkerId} shut down", _workerId);
    }

    private async Task ProcessNextTaskAsync(List<string> supportedTypes, CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<ITaskQueueService>();
        var registry = scope.ServiceProvider.GetRequiredService<IWorkerRegistryService>();

        var task = await queueService.DequeueAsync(_workerId, supportedTypes, stoppingToken);
        if (task == null)
        {
            await Task.Delay(500, stoppingToken);
            return;
        }

        _logger.LogInformation("Processing task {TaskId} ({Type})", task.Id, task.Type);
        await registry.UpdateHeartbeatAsync(_workerId, WorkerStatus.Busy, task.Id);

        var handlers = scope.ServiceProvider.GetServices<ITaskHandler>();
        var handler = handlers.FirstOrDefault(h => h.TaskType == task.Type);

        if (handler == null)
        {
            _logger.LogWarning("No handler for task type {Type}", task.Type);
            await queueService.CompleteTaskAsync(new TaskResult
            {
                TaskId = task.Id,
                Success = false,
                ErrorMessage = $"No handler registered for type '{task.Type}'"
            });
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(task.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

            var result = await handler.ExecuteAsync(task, linkedCts.Token);
            sw.Stop();

            result.DurationMs = sw.ElapsedMilliseconds;
            await queueService.CompleteTaskAsync(result);

            _logger.LogInformation("Task {TaskId} completed in {Ms}ms", task.Id, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogWarning("Task {TaskId} timed out after {Seconds}s", task.Id, task.TimeoutSeconds);
            await queueService.UpdateTaskStatusAsync(task.Id, Shared.Models.TaskStatus.TimedOut, errorMessage: "Task timed out");

            if (task.RetryCount < task.MaxRetries)
                await queueService.RequeueFailedTaskAsync(task.Id);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Task {TaskId} failed", task.Id);

            await queueService.CompleteTaskAsync(new TaskResult
            {
                TaskId = task.Id,
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            });

            if (task.RetryCount < task.MaxRetries)
                await queueService.RequeueFailedTaskAsync(task.Id);
        }
        finally
        {
            await registry.UpdateHeartbeatAsync(_workerId, WorkerStatus.Idle);
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<IWorkerRegistryService>();
                var memMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                await registry.UpdateHeartbeatAsync(_workerId, WorkerStatus.Idle, memoryMb: memMb);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed");
            }

            await Task.Delay(10000, stoppingToken);
        }
    }
}
