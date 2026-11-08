using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/otp")]
public sealed class OtpController : ControllerBase
{
    private readonly OtpService _otp;

    public OtpController(OtpService otp)
    {
        _otp = otp;
    }

    [HttpGet("requirements")]
    public async Task<ActionResult<OtpRequirementsDto>> GetRequirements()
        => Ok(await _otp.GetRequirementsAsync());

    [HttpPost("send")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<OtpResultDto>> Send([FromBody] SendOtpByPurposeRequest request)
        => Ok(await _otp.SendAsync(request.Mobile, request.Purpose));

    [HttpPost("verify")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<OtpVerifyResultDto>> Verify([FromBody] VerifyOtpByPurposeRequest request)
        => Ok(await _otp.VerifyAsync(request.Mobile, request.Purpose, request.Code, consume: false));
}
