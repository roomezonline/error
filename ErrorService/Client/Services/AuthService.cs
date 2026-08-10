using ErrorService.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ErrorService.Client.Services;

public class AuthService
{
    private const string TokenKey = "auth_token";

    private readonly HttpClient _http;
    private readonly LocalStorageService _storage;
    private readonly IServiceProvider _serviceProvider;

    public AuthService(HttpClient http, LocalStorageService storage, IServiceProvider serviceProvider)
    {
        _http = http;
        _storage = storage;
        _serviceProvider = serviceProvider;
    }

    private JwtAuthStateProvider AuthStateProvider =>
        (JwtAuthStateProvider)_serviceProvider.GetRequiredService(typeof(AuthenticationStateProvider));

    public async Task<string?> GetTokenAsync() => await _storage.GetAsync(TokenKey);
    public async Task<string?> GetWorkshopTokenAsync() => await _storage.GetAsync("workshop_token");

    public async Task InitializeAsync()
    {
        var token = await GetTokenAsync() ?? await GetWorkshopTokenAsync();
        ApplyTokenToHttp(token);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/register", request);
        var body = await ReadOrThrow<AuthResponse>(resp);
        await PersistTokenAsync(body.Token);
        return body;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login", request);
        var body = await ReadOrThrow<AuthResponse>(resp);
        await PersistTokenAsync(body.Token);
        return body;
    }

    public async Task<UnifiedAuthResponse> UnifiedLoginAsync(LoginRequest request)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/unified-login", request);
        var body = await ReadOrThrow<UnifiedAuthResponse>(resp);

        var storageKey = body.UserType == "workshop" ? "workshop_token" : TokenKey;
        await _storage.SetAsync(storageKey, body.Token);
        ApplyTokenToHttp(body.Token);
        AuthStateProvider.NotifyAuthStateChanged();

        return body;
    }

    public async Task LogoutAsync()
    {
        await _storage.RemoveAsync(TokenKey);
        await _storage.RemoveAsync("workshop_token");
        ApplyTokenToHttp(null);
        AuthStateProvider.NotifyAuthStateChanged();
    }

    public async Task<UserProfileDto?> GetMeAsync()
    {
        var resp = await _http.GetAsync("api/auth/me");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<UserProfileDto>();
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var resp = await _http.PutAsJsonAsync("api/auth/profile", request);
        return resp.IsSuccessStatusCode;
    }

    public async Task<(bool ok, string? error)> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var resp = await _http.PutAsJsonAsync("api/auth/change-password", request);
        if (resp.IsSuccessStatusCode) return (true, null);
        var msg = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(msg) ? "خطا در تغییر رمز" : msg.Trim('"'));
    }

    private async Task PersistTokenAsync(string token)
    {
        await _storage.SetAsync(TokenKey, token);
        ApplyTokenToHttp(token);
        AuthStateProvider.NotifyAuthStateChanged();
    }

    private void ApplyTokenToHttp(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<T> ReadOrThrow<T>(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode)
        {
            var ok = await resp.Content.ReadFromJsonAsync<T>();
            if (ok == null) throw new InvalidOperationException("Empty response");
            return ok;
        }

        var text = await resp.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "خطا در ارتباط با سرور" : text.Trim('"'));
    }
}
