using ErrorService.Server.Infrastructure;
using ErrorService.Server.Data;
using ErrorService.Server.Data.Seed;
using ErrorService.Server.Hubs;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.ChatAi;
using ErrorService.Server.Services.Messenger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream", "application/xml", "text/xml" });
});

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SystemEventService>();
builder.Services.AddSingleton<MonitoringCacheService>();
builder.Services.AddScoped<MonitoringAuthorizationService>();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ErrorService.Server.Services.Payment.IPaymentGatewayFactory, ErrorService.Server.Services.Payment.PaymentGatewayFactory>();

builder.Services.AddScoped<ErrorService.Server.Services.Backup.IBackupService, ErrorService.Server.Services.Backup.SqlServerBackupService>();
builder.Services.AddSingleton<ErrorService.Server.Services.IEmailService, ErrorService.Server.Services.SmtpEmailService>();
builder.Services.AddHostedService<ErrorService.Server.Services.Backup.BackupBackgroundService>();
var redisConnection = builder.Configuration.GetSection("Redis")["ConnectionString"];
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection, options =>
    {
        options.ConnectionFactory = async _ => await ConnectionMultiplexer.ConnectAsync(redisConnection);
    });
    Console.WriteLine("SignalR: Redis backplane configured");
}
else
{
    builder.Services.AddSignalR();
    Console.WriteLine("SignalR: using in-process backplane (no Redis)");
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddScoped<BaleBotService>();
builder.Services.AddScoped<IChatMessengerChannel, BaleMessengerChannel>();
builder.Services.AddScoped<IChatMessengerChannel, TelegramMessengerChannel>();
builder.Services.AddScoped<IChatMessengerChannel, EitaaMessengerChannel>();
builder.Services.AddScoped<MessengerRouter>();
builder.Services.AddScoped<IChatAiService, FaqChatService>();
builder.Services.AddScoped<IChatAiService, OpenAiCompatibleChatService>();
builder.Services.AddScoped<ChatAiCoordinator>();

var jwtKey = builder.Configuration["Auth:JwtKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Missing Auth:JwtKey in configuration.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddOutputCache(options =>
{
    options.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(60);
});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is missing. Add it to appsettings.Development.json (ConnectionStrings:DefaultConnection).");
}

builder.Services.AddDbContext<ErrorServiceDbContext>(options =>
    options
        .UseSqlServer(connectionString)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// SSL settings holder — populated from DB at startup, read by middleware at runtime
var sslSettingsHolder = new SslSettingsHolder();
builder.Services.AddSingleton(sslSettingsHolder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

if (app.Environment.IsDevelopment())
{
    if (app.Configuration.GetValue<bool>("EnableWasmDebugging"))
    {
        app.UseWebAssemblyDebugging();
    }
    app.UseDeveloperExceptionPage();
}

// WWW → non-www redirect (before SSL)
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host;
    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
    {
        var nonWww = host[4..];
        var redirectUrl = $"{context.Request.Scheme}://{nonWww}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect(redirectUrl, permanent: true);
        return;
    }
    await next();
});

// Old WebForms URL redirects
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var lower = path.ToLowerInvariant();

    if (lower == "/search.aspx" || lower.StartsWith("/search.aspx?"))
    {
        var query = context.Request.QueryString.Value ?? "";
        var searchMatch = System.Text.RegularExpressions.Regex.Match(query, @"str=([^&]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (searchMatch.Success)
        {
            var term = Uri.UnescapeDataString(searchMatch.Groups[1].Value);
            context.Response.Redirect($"/search?q={Uri.EscapeDataString(term)}", permanent: true);
        }
        else
        {
            context.Response.Redirect("/search", permanent: true);
        }
        return;
    }

    if (lower is "/index.aspx" or "/default.aspx" or "/default.asp")
    {
        context.Response.Redirect("/", permanent: true);
        return;
    }

    await next();
});

// Conditional SSL / HSTS middleware (reads EnableSsl from DB via SslSettingsHolder)
app.Use(async (context, next) =>
{
    var holder = context.RequestServices.GetRequiredService<SslSettingsHolder>();
    if (holder.EnableSsl)
    {
        if (context.Request.IsHttps)
        {
            context.Response.Headers.StrictTransportSecurity = $"max-age={holder.HstsMaxAgeDays * 86400}";
        }
        else
        {
            var httpsUrl = $"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect(httpsUrl, permanent: true);
            return;
        }
    }
    await next();
});

// Normalize trailing slashes for non-API, non-file paths
app.UseMiddleware<ErrorService.Server.Infrastructure.TrailingSlashMiddleware>();

// POST-based method override for hosts that block PUT/DELETE (WebDAV on IIS)
app.Use(async (context, next) =>
{
    if (string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase)
        && context.Request.Headers.TryGetValue("X-HTTP-Method-Override", out var method)
        && (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)))
    {
        context.Request.Method = method!;
    }
    await next();
});

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalException");
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled exception. Method={Method} Path={Path} Query={QueryString}", context.Request.Method, context.Request.Path, context.Request.QueryString);
        // Return a proper error response instead of re-throwing
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync("<!DOCTYPE html><html lang=\"fa\" dir=\"rtl\"><head><meta charset=\"utf-8\"/><title>خطای سرور | ارورسرویس</title><meta name=\"robots\" content=\"noindex, nofollow\"/><style>body{font-family:Vazirmatn,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#f8fafc;color:#1e293b;direction:rtl;}.e{text-align:center;padding:40px;}.e h1{font-size:72px;margin:0;color:#ef4444;}.e h2{margin-top:8px;font-size:24px;}.e p{color:#64748b;margin-top:16px;}</style></head><body><div class=\"e\"><h1>500</h1><h2>خطای داخلی سرور</h2><p>متاسفانه خطایی رخ داده است. لطفاً چند دقیقه دیگر دوباره تلاش کنید.</p></div></body></html>");
        return;
    }
});

app.UseSystemEventTracker();

app.UseResponseCompression();

app.UseCors("AllowBlazor");

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ErrorService.Server.Infrastructure.SeoFallbackMiddleware>();

app.UseOutputCache();

var applyMigrationsOnStartup = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
var seedAuthorization = app.Configuration.GetValue("Database:SeedAuthorization", true);
var seedLocations = app.Configuration.GetValue("Database:SeedLocations", false);
if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        logger.LogInformation("Applying EF Core migrations...");
        db.Database.Migrate();
        logger.LogInformation("EF Core migrations applied successfully.");

        if (seedAuthorization)
        {
            try
            {
                var authzLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AuthorizationSeeder");
                await AuthorizationSeeder.SeedAsync(db, authzLogger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to seed roles/permissions.");
            }
        }

        // همیشه چک می‌کنیم، اگر جداول لوکیشن خالی بودند پر می‌کنیم (بدون وابستگی به کانفیگ خارجی برای اطمینان بیشتر)
        try
        {
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LocationSeeder");
            await LocationSeeder.SeedAsync(db, env, seedLogger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed provinces/cities.");
        }

        // Read SSL settings from DB
        try
        {
            var settings = await db.SiteSettings.FirstOrDefaultAsync();
            if (settings != null)
            {
                sslSettingsHolder.EnableSsl = settings.EnableSsl;
                sslSettingsHolder.HstsMaxAgeDays = settings.HstsMaxAgeDays;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read SSL settings, using defaults.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply EF Core migrations on startup.");
        throw;
    }
}
else
{
    // Read SSL settings from DB even when migrations are skipped
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var settings = await db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
        {
            sslSettingsHolder.EnableSsl = settings.EnableSsl;
            sslSettingsHolder.HstsMaxAgeDays = settings.HstsMaxAgeDays;
        }
    }
    catch { /* use defaults */ }
}

app.MapHub<ErrorService.Server.Hubs.ChatHub>("/hubs/chat");
app.MapRazorPages();
app.MapControllers();

app.MapGet("/service-worker.js", (IWebHostEnvironment webEnv) =>
{
    var wwwroot = webEnv.WebRootPath ?? Path.Combine(webEnv.ContentRootPath, "wwwroot");
    var path = Path.Combine(wwwroot, "service-worker.js");
    if (!File.Exists(path)) path = Path.Combine(webEnv.ContentRootPath, "service-worker.js");
    return File.Exists(path) ? Results.File(path, "text/javascript") : Results.NotFound();
});

app.MapFallbackToFile("index.html");

app.Run();

public sealed class SslSettingsHolder
{
    public bool EnableSsl { get; set; } = true;
    public int HstsMaxAgeDays { get; set; } = 7;
}
