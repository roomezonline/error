using System.Text.Json;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services.Payment;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IPaymentGatewayFactory _gatewayFactory;

    public PaymentController(ErrorServiceDbContext db, IPaymentGatewayFactory gatewayFactory)
    {
        _db = db;
        _gatewayFactory = gatewayFactory;
    }

    [AllowAnonymous]
    [HttpGet("info")]
    public async Task<ActionResult<PaymentInfoDto>> GetInfo(CancellationToken ct)
        => Ok(await _gatewayFactory.GetInfoAsync(ct));

    [AllowAnonymous]
    [HttpGet("gateways")]
    public async Task<ActionResult<List<PaymentGatewayListItemDto>>> GetOnlineGateways(CancellationToken ct)
        => Ok(await _gatewayFactory.GetOnlineGatewaysAsync(ct));

    [Authorize(Policy = "perm:admin.shop.gateway.view")]
    [HttpGet("providers")]
    public ActionResult<List<PaymentProviderDefinition>> GetProviders()
        => Ok(PaymentProviderCatalog.All);

    [Authorize(Policy = "perm:admin.shop.gateway.manage")]
    [HttpGet("admin/gateways")]
    public async Task<ActionResult<List<PaymentGatewayDto>>> GetAdminGateways(CancellationToken ct)
    {
        await _gatewayFactory.EnsureCatalogAsync(ct);
        var gateways = await _db.PaymentGateways.AsNoTracking()
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Id)
            .ToListAsync(ct);
        return Ok(gateways.Select(PaymentGatewayFactory.MapToDto).ToList());
    }

    [Authorize(Policy = "perm:admin.shop.gateway.manage")]
    [HttpPost("admin/gateways")]
    public async Task<ActionResult<PaymentGatewayDto>> CreateGateway([FromBody] PaymentGatewayCreateRequest request, CancellationToken ct)
    {
        var provider = request.Provider?.Trim().ToLowerInvariant();
        var def = PaymentProviderCatalog.Find(provider);
        if (def == null)
            return BadRequest("درگاه انتخابی معتبر نیست.");

        if (await _db.PaymentGateways.AnyAsync(g => g.Provider == def.Key, ct))
            return BadRequest("این درگاه قبلاً تعریف شده است.");

        var gateway = new PaymentGateway
        {
            Provider = def.Key,
            Title = def.Title,
            IsActive = false,
            IsConfigured = !def.NeedsConfig,
            Sandbox = true,
            SortOrder = await _db.PaymentGateways.MaxAsync(g => (int?)g.SortOrder, ct) + 1 ?? 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.PaymentGateways.Add(gateway);
        await _db.SaveChangesAsync(ct);
        return Ok(PaymentGatewayFactory.MapToDto(gateway));
    }

    [Authorize(Policy = "perm:admin.shop.gateway.manage")]
    [HttpPut("admin/gateways/{id:int}")]
    public async Task<ActionResult<PaymentGatewayDto>> SaveGateway(int id, [FromBody] PaymentGatewaySaveRequest request, CancellationToken ct)
    {
        var gateway = await _db.PaymentGateways.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gateway == null)
            return NotFound();

        var def = PaymentProviderCatalog.Find(gateway.Provider);
        if (def == null)
            return BadRequest("درگاه انتخابی معتبر نیست.");

        gateway.Title = string.IsNullOrWhiteSpace(request.Title) ? def.Title : request.Title.Trim();
        gateway.Sandbox = request.Sandbox;
        gateway.IsActive = request.IsActive;
        gateway.CallbackBaseUrl = string.IsNullOrWhiteSpace(request.CallbackBaseUrl) ? null : request.CallbackBaseUrl.Trim();
        gateway.SortOrder = request.SortOrder;
        gateway.ConfigJson = SerializeConfig(def, request.Config);
        gateway.IsConfigured = IsConfigured(def, request.Config);
        gateway.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(PaymentGatewayFactory.MapToDto(gateway));
    }

    [Authorize(Policy = "perm:admin.shop.gateway.manage")]
    [HttpDelete("admin/gateways/{id:int}")]
    public async Task<IActionResult> DeleteGateway(int id, CancellationToken ct)
    {
        var gateway = await _db.PaymentGateways.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gateway == null)
            return NotFound();

        var providerEnum = PaymentProviderCatalog.TryToEnum(gateway.Provider);
        if (providerEnum.HasValue && await _db.Orders.AnyAsync(o => o.PaymentProvider == providerEnum.Value, ct))
            return BadRequest("این درگاه دارای سابقه سفارش است و قابل حذف نیست؛ می‌توانید آن را غیرفعال کنید.");

        _db.PaymentGateways.Remove(gateway);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("simulate/{orderId:int}")]
    public IActionResult Simulate(int orderId, [FromQuery] string? authority, [FromQuery] decimal? amount)
    {
        var a = string.IsNullOrWhiteSpace(authority) ? Guid.NewGuid().ToString("N") : authority;
        var amt = amount ?? 0;
        return Redirect($"/api/orders/{orderId}/payment/callback?Status=OK&Authority={Uri.EscapeDataString(a)}&Amount={amt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
    }

    private static bool IsConfigured(PaymentProviderDefinition def, Dictionary<string, string>? config)
    {
        if (!def.NeedsConfig) return true;
        if (config == null) return false;
        return def.Fields.Where(f => f.Required).All(f =>
            config.TryGetValue(f.Key, out var value) && !string.IsNullOrWhiteSpace(value));
    }

    private static string? SerializeConfig(PaymentProviderDefinition def, Dictionary<string, string>? config)
    {
        if (config == null || config.Count == 0) return null;
        var allowedKeys = def.Fields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clean = config
            .Where(kv => allowedKeys.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim());
        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }
}