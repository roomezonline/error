namespace ErrorService.Client.Services;

/// <summary>
/// رویداد سراسری انقضای نشست. AuthHttpHandler هنگام دیدن توکن منقضی آن را
/// فعال می‌کند و SessionExpiryWatcher در هر دو چیدمان (سایت و ادمین) کاربر را
/// به صفحه ورود هدایت می‌کند. این واسطه لازم است چون هندلر HTTP نمی‌تواند به
/// AuthService تزریق شود (وابستگی چرخه‌ای).
/// </summary>
public sealed class SessionExpiryNotifier
{
    public event Action? Expired;

    public void NotifyExpired() => Expired?.Invoke();
}
