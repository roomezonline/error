using Microsoft.JSInterop;

namespace ErrorService.Client.Services
{
    public class ThemeService
    {
        private readonly IJSRuntime _js;
        private bool _isDarkMode;

        public ThemeService(IJSRuntime js)
        {
            _js = js;
        }

        public bool IsDarkMode => _isDarkMode;

        public async Task InitializeAsync()
        {
            try
            {
                var theme = await _js.InvokeAsync<string>("localStorage.getItem", "theme");
                _isDarkMode = theme == "dark";
                await ApplyTheme();
            }
            catch (JSException)
            {
                // Pre-rendering has no JS runtime; the client picks the theme up on first paint.
            }
        }

        public async Task ToggleThemeAsync()
        {
            _isDarkMode = !_isDarkMode;
            await _js.InvokeVoidAsync("localStorage.setItem", "theme", _isDarkMode ? "dark" : "light");
            await ApplyTheme();
            NotifyStateChanged();
        }

        /// <summary>لایهٔ دارک صفحات عمومی با اولین ورود به حالت تاریک بارگذاری می‌شود.</summary>
        private async Task ApplyTheme()
        {
            await _js.InvokeVoidAsync("esApplyTheme", _isDarkMode);
        }

        /// <summary>لایهٔ دارک مخصوص پنل مدیریت فقط در حالت تاریک و در صفحات ادمین بارگذاری می‌شود.</summary>
        public async Task EnsureAdminCssAsync()
        {
            if (!_isDarkMode) return;
            try
            {
                await _js.InvokeVoidAsync("esLoadCssOnce", "/css/dark-admin.css");
            }
            catch (JSException) { }
        }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
