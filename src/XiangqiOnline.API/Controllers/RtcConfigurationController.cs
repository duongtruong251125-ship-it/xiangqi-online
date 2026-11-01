using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Contracts;

namespace XiangqiOnline.API.Controllers;

[ApiController, Authorize, Route("api/v1/rtc")]
public sealed class RtcConfigurationController(IOptions<WebRtcOptions> options) : ControllerBase
{
    [HttpGet("ice-servers")]
    public IActionResult GetIceServers() => Ok(ApiResponses.Success(options.Value.IceServers, HttpContext));
}
