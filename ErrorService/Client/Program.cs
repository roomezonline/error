using ErrorService.Client;
using ErrorService.Client.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddScoped<AuthHttpHandler>();
builder.Services.AddScoped<MethodOverrideHandler>();
builder.Services.AddScoped<ClientCachingHandler>();
builder.Services.AddScoped(sp =>
{
    var authHandler = sp.GetRequiredService<AuthHttpHandler>();
    authHandler.InnerHandler = new HttpClientHandler();

    var cacheHandler = sp.GetRequiredService<ClientCachingHandler>();
    cacheHandler.InnerHandler = authHandler;

    var overrideHandler = sp.GetRequiredService<MethodOverrideHandler>();
    overrideHandler.InnerHandler = cacheHandler;

    return new HttpClient(overrideHandler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<RecentlyViewedService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<AdminWorkshopSelectionService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ClientErrorLogger>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

var host = builder.Build();

// Apply persisted token (if any) to HttpClient before first requests
await host.Services.GetRequiredService<AuthService>().InitializeAsync();

await host.RunAsync();
