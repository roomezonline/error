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

    [HttpPost("settings")]
    public async Task<ActionResult<HostInfoDto>> SaveSettings([FromBody] HostQuotaUpdateRequest request)
    {
        if (double.IsNaN(request.QuotaGb) || double.IsInfinity(request.QuotaGb))
            return BadRequest(new { message = "مقدار فضای کل معتبر نیست." });

        if (request.QuotaGb < 0 || request.QuotaGb > 1_048_576)
            return BadRequest(new { message = "فضای کل باید بین صفر تا ۱٬۰۴۸٬۵۷۶ گیگابایت باشد." });

        await _host.SaveQuotaGbAsync(request.QuotaGb);
        return Ok(_host.GetSnapshot());
    }
}

public sealed record HostQuotaUpdateRequest(double QuotaGb);
