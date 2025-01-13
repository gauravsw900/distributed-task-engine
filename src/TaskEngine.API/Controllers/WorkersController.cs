using Microsoft.AspNetCore.Mvc;
using TaskEngine.Core.Services;
using TaskEngine.Shared.Models;

namespace TaskEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WorkersController : ControllerBase
{
    private readonly IWorkerRegistryService _workerRegistry;
    private readonly ILogger<WorkersController> _logger;

    public WorkersController(IWorkerRegistryService workerRegistry, ILogger<WorkersController> logger)
    {
        _workerRegistry = workerRegistry;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkerInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkers()
    {
        var workers = await _workerRegistry.GetActiveWorkersAsync();
        return Ok(workers);
    }

    [HttpGet("{workerId}")]
    [ProducesResponseType(typeof(WorkerInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorker(string workerId)
    {
        var worker = await _workerRegistry.GetWorkerAsync(workerId);
        return worker == null ? NotFound() : Ok(worker);
    }
}
