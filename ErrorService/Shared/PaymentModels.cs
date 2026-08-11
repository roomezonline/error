namespace ErrorService.Shared;

public enum PaymentProvider
{
    Manual = 0,
    Zarinpal = 1,
    IdPay = 2,
    Zibal = 3,
    Virtual = 4
}

public class PaymentIntentRequest
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Description { get; set; }
    public string? CallbackUrl { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
}

public class PaymentIntentResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Authority { get; set; }
    public string? GatewayUrl { get; set; }
    public string PaymentMode { get; set; } = "Manual";
}

public class PaymentCallbackPayload
{
    public int OrderId { get; set; }
    public string? Authority { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public string? RefId { get; set; }
}

public class PaymentInfoDto
{
    public bool OnlineEnabled { get; set; }
    public int OnlineGatewayCount { get; set; }
    public string PrimaryProvider { get; set; } = "manual";
    public string PrimaryProviderLabel { get; set; } = "پرداخت کارت‌به‌کارت";
}

public class GatewayConfigField
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Placeholder { get; set; }
    public bool IsSecret { get; set; }
    public bool Required { get; set; }
}

public class PaymentProviderDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? DocsUrl { get; set; }
    public bool Online { get; set; }
    public bool SupportsSandbox { get; set; }
    public string? SandboxHint { get; set; }
    public bool NeedsConfig { get; set; }
    public List<GatewayConfigField> Fields { get; set; } = new();
}

public class PaymentGatewayListItemDto
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Online { get; set; }
}

public class PaymentGatewayDto
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Online { get; set; }
    public bool IsActive { get; set; }
    public bool IsConfigured { get; set; }
    public bool Sandbox { get; set; }
    public bool SupportsSandbox { get; set; }
    public string? SandboxHint { get; set; }
    public string? CallbackBaseUrl { get; set; }
    public string? CallbackUrl { get; set; }
    public int SortOrder { get; set; }
    public Dictionary<string, string> Config { get; set; } = new();
    public List<GatewayConfigField> Fields { get; set; } = new();
}

public class PaymentGatewaySaveRequest
{
    public int? Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool Sandbox { get; set; } = true;
    public string? CallbackBaseUrl { get; set; }
    public int SortOrder { get; set; }
    public Dictionary<string, string> Config { get; set; } = new();
}

public class PaymentGatewayCreateRequest
{
    public string Provider { get; set; } = string.Empty;
}