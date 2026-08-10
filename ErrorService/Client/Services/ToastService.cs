namespace ErrorService.Client.Services;

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class ToastService
{
    public event Action<string, ToastLevel>? OnShow;

    public void ShowInfo(string message) => OnShow?.Invoke(message, ToastLevel.Info);
    public void ShowSuccess(string message) => OnShow?.Invoke(message, ToastLevel.Success);
    public void ShowWarning(string message) => OnShow?.Invoke(message, ToastLevel.Warning);
    public void ShowError(string message) => OnShow?.Invoke(message, ToastLevel.Error);
}
