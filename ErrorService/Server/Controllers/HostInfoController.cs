using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/hostinfo")]
[Authorize(Policy = "perm:admin.hostinfo.view")]
public sealed class HostInfoController : ControllerBase
{
    private readonly HostInfoService _host;

    public HostInfoController(HostInfoService host)
    {
        _host = host;
    }

    [HttpGet]
    public ActionResult<HostInfoDto> Get()
    {
        return Ok(_host.GetSnapshot());
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<HostInfoDto>> Refresh()
    {
        await _host.RefreshAsync();
        return Ok(_host.GetSnapshot());
    }
}
