using System.Text.Json;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Payment;

public interface IPaymentGatewayFactory
{
    Task EnsureCatalogAsync(CancellationToken ct = default);
    Task<List<PaymentGatewayListItemDto>> GetOnlineGatewaysAsync(CancellationToken ct = default);
    Task<PaymentGatewayDto?> GetGatewayDtoAsync(int id, CancellationToken ct = default);
    Task<IShopPaymentGateway?> GetGatewayByIdAsync(int id, CancellationToken ct = default);
    Task<IShopPaymentGateway?> GetGatewayByProviderAsync(string provider, CancellationToken ct = default);
    Task<PaymentInfoDto> GetInfoAsync(CancellationToken ct = default);
}

public sealed class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly ErrorServiceDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public PaymentGatewayFactory(ErrorServiceDbContext db, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    public async Task EnsureCatalogAsync(CancellationToken ct = default)
    {
        foreach (var def in PaymentProviderCatalog.All)
        {
            if (await _db.PaymentGateways.AnyAsync(g => g.Provider == def.Key, ct))
                continue;

            var manual = def.Key == "manual";
            _db.PaymentGateways.Add(new PaymentGateway
            {
                Provider = def.Key,
                Title = def.Title,
                IsActive = manual,
                IsConfigured = !def.NeedsConfig,
                Sandbox = true,
                SortOrder = 0
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<PaymentGatewayListItemDto>> GetOnlineGatewaysAsync(CancellationToken ct = default)
    {
        await EnsureCatalogAsync(ct);
        return await _db.PaymentGateways
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Id)
            .ToListAsync(ct)
            .ContinueWith(t =>
            {
                var list = new List<PaymentGatewayListItemDto>();
                foreach (var g in t.Result)
                {
                    var def = PaymentProviderCatalog.Find(g.Provider);
                    if (def == null || !def.Online) continue;
                    if (def.NeedsConfig && !g.IsConfigured) continue;
                    list.Add(new PaymentGatewayListItemDto
                    {
                        Id = g.Id,
                        Provider = g.Provider,
                        Title = string.IsNullOrWhiteSpace(g.Title) ? def.Title : g.Title,
                        Online = true
                    });
                }
                return list;
            }, ct);
    }

    public async Task<PaymentGatewayDto?> GetGatewayDtoAsync(int id, CancellationToken ct = default)
    {
        var row = await _db.PaymentGateways.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (row == null) return null;
        return MapToDto(row);
    }

    public async Task<IShopPaymentGateway?> GetGatewayByIdAsync(int id, CancellationToken ct = default)
    {
        var row = await _db.PaymentGateways.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (row == null) return null;

        var def = PaymentProviderCatalog.Find(row.Provider);
        if (def == null || !def.Online || !row.IsActive) return null;
        if (def.NeedsConfig && !row.IsConfigured) return null;

        return BuildGateway(row);
    }

    public async Task<IShopPaymentGateway?> GetGatewayByProviderAsync(string provider, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider)) return null;
        var key = provider.Trim().ToLowerInvariant();
        var row = await _db.PaymentGateways.AsNoTracking().FirstOrDefaultAsync(g => g.Provider == key && g.IsActive, ct);
        if (row == null) return null;
        return BuildGateway(row);
    }

    public async Task<PaymentInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        var online = await GetOnlineGatewaysAsync(ct);
        var primary = online.FirstOrDefault()?.Title ?? "پرداخت کارت‌به‌کارت";
        return new PaymentInfoDto
        {
            OnlineEnabled = online.Count > 0,
            OnlineGatewayCount = online.Count,
            PrimaryProvider = online.FirstOrDefault()?.Provider ?? "manual",
            PrimaryProviderLabel = primary
        };
    }

    private IShopPaymentGateway? BuildGateway(PaymentGateway row)
        => row.Provider.ToLowerInvariant() switch
        {
            "zarinpal" => new ZarinpalPaymentGateway(row, _httpClientFactory, _loggerFactory.CreateLogger<ZarinpalPaymentGateway>()),
            "zibal" => new ZibalPaymentGateway(row, _httpClientFactory, _loggerFactory.CreateLogger<ZibalPaymentGateway>()),
            "virtual" => new VirtualPaymentGateway(),
            _ => null
        };

    public static PaymentGatewayDto MapToDto(PaymentGateway row)
    {
        var def = PaymentProviderCatalog.Find(row.Provider);
        var config = ParseConfig(row.ConfigJson);
        return new PaymentGatewayDto
        {
            Id = row.Id,
            Provider = row.Provider,
            Title = string.IsNullOrWhiteSpace(row.Title) ? def?.Title ?? row.Provider : row.Title,
            Online = def?.Online ?? false,
            SupportsSandbox = def?.SupportsSandbox ?? false,
            SandboxHint = def?.SandboxHint,
            IsActive = row.IsActive,
            IsConfigured = row.IsConfigured,
            Sandbox = row.Sandbox,
            CallbackBaseUrl = row.CallbackBaseUrl,
            CallbackUrl = BuildCallbackUrl(row.CallbackBaseUrl),
            SortOrder = row.SortOrder,
            Config = config,
            Fields = def?.Fields ?? new List<GatewayConfigField>()
        };
    }

    public static Dictionary<string, string> ParseConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(configJson)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    public static string? GetConfigValue(PaymentGateway row, string key)
    {
        var config = ParseConfig(row.ConfigJson);
        return config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    public static string BuildCallbackUrl(string? callbackBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(callbackBaseUrl))
            return "/api/orders/{orderId}/payment/callback";
        var baseUrl = callbackBaseUrl.TrimEnd('/');
        return baseUrl.Contains("{orderId}", StringComparison.Ordinal)
            ? baseUrl
            : $"{baseUrl}/api/orders/{{orderId}}/payment/callback";
    }
}