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
            var theme = await _js.InvokeAsync<string>("localStorage.getItem", "theme");
            _isDarkMode = theme == "dark";
            await ApplyTheme();
        }

        public async Task ToggleThemeAsync()
        {
            _isDarkMode = !_isDarkMode;
            await _js.InvokeVoidAsync("localStorage.setItem", "theme", _isDarkMode ? "dark" : "light");
            await ApplyTheme();
            NotifyStateChanged();
        }

        private async Task ApplyTheme()
        {
            await _js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", _isDarkMode ? "dark" : "light");
        }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
