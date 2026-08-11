namespace ErrorService.Shared;

/// <summary>
/// Standard catalog of supported payment providers/gateways plus enum mapping.
/// Adding a new gateway = add one definition here (fields + online flag) and
/// an IShopPaymentGateway implementation resolving that key in the factory.
/// </summary>
public static class PaymentProviderCatalog
{
    public static readonly IReadOnlyList<PaymentProviderDefinition> All = new List<PaymentProviderDefinition>
    {
        new()
        {
            Key = "manual",
            Title = "کارت‌به‌کارت",
            Description = "واریز مستقیم به حساب‌های بانکی و آپلود رسید. بدون نیاز به تنظیمات اضافه.",
            Online = false,
            NeedsConfig = false
        },
        new()
        {
            Key = "zarinpal",
            Title = "زرین‌پال",
            Description = "درگاه پرداخت آنلاین زرین‌پال. نیازمند شناسه مرچنت و ثبت آدرس بازگشت.",
            DocsUrl = "https://docs.zarinpal.com/",
            Online = true,
            NeedsConfig = true,
            SupportsSandbox = true,
            SandboxHint = "در حالت آزمایشگاهی (Sandbox) مبلغ واقعی از حساب مشتری کسر نمی‌شود.",
            Fields = new List<GatewayConfigField>
            {
                new()
                {
                    Key = "merchant_id",
                    Label = "شناسه مرچنت (Merchant ID)",
                    Placeholder = "1ab2cd34-....-....-....",
                    IsSecret = true,
                    Required = true
                }
            }
        },
        new()
        {
            Key = "zibal",
            Title = "زیبال",
            Description = "درگاه پرداخت آنلاین زیبال. نیازمند کد درگاه (مرچنت).",
            DocsUrl = "https://docs.zibal.ir/",
            Online = true,
            NeedsConfig = true,
            SandboxHint = "زیبال درگاه آزمایشی مستقل ندارد؛ با کد «zibal» تراکنش تستی انجام می‌شود.",
            Fields = new List<GatewayConfigField>
            {
                new()
                {
                    Key = "merchant",
                    Label = "کد درگاه (merchant)",
                    Placeholder = "zibal",
                    IsSecret = true,
                    Required = true
                }
            }
        },
        new()
        {
            Key = "virtual",
            Title = "درگاه تست (مجازی)",
            Description = "شبیه‌سازی پرداخت آنلاین برای تست داخلی؛ بدون درگاه واقعی.",
            Online = true,
            NeedsConfig = false
        }
    };

    public static PaymentProviderDefinition? Find(string? key)
        => All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string ToProviderKey(PaymentProvider provider)
        => provider switch
        {
            PaymentProvider.Manual => "manual",
            PaymentProvider.Zarinpal => "zarinpal",
            PaymentProvider.Zibal => "zibal",
            PaymentProvider.Virtual => "virtual",
            _ => "idpay"
        };

    public static PaymentProvider? TryToEnum(string? key)
    {
        key = key?.Trim().ToLowerInvariant();
        return key switch
        {
            "manual" => PaymentProvider.Manual,
            "zarinpal" => PaymentProvider.Zarinpal,
            "zibal" => PaymentProvider.Zibal,
            "virtual" => PaymentProvider.Virtual,
            "idpay" => PaymentProvider.IdPay,
            _ => null
        };
    }
}