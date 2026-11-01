using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XiangqiOnline.API.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
        var payload = new { status = report.Status.ToString(), checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() }) };
        return report.Status == HealthStatus.Healthy ? Ok(payload) : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }
}
