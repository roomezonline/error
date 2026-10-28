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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;

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
    // Brotli = نسبت فشرده‌سازی بالاتر برای موبایل؛ gzip برای کلاینت‌های قدیمی‌تر.
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream", "application/xml", "text/xml", "application/wasm", "application/font-woff2", "font/woff2", "image/svg+xml", "application/manifest+json" });
});

// سریع‌ترین سطح: کمترین تأخیر پاسخ، نه بیشترین نسبت فشرده‌سازی.
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SystemEventService>();
builder.Services.AddScoped<NotificationEventService>();
builder.Services.AddScoped<StockAlertNotifier>();
builder.Services.AddScoped<AdminOrderService>();
builder.Services.AddSingleton<MonitoringCacheService>();
builder.Services.AddScoped<MonitoringAuthorizationService>();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ErrorService.Server.Services.Payment.IPaymentGatewayFactory, ErrorService.Server.Services.Payment.PaymentGatewayFactory>();

builder.Services.AddScoped<ErrorService.Server.Services.Backup.IBackupService, ErrorService.Server.Services.Backup.SqlServerBackupService>();
builder.Services.AddSingleton<ErrorService.Server.Services.IEmailService, ErrorService.Server.Services.SmtpEmailService>();
builder.Services.AddHostedService<ErrorService.Server.Services.Backup.BackupBackgroundService>();
builder.Services.AddHostedService<ErrorService.Server.Services.PendingOrderCleanupService>();
builder.Services.AddHostedService<ErrorService.Server.Services.MonitoringExpiryNotificationService>();
builder.Services.AddHostedService<ErrorService.Server.Services.MonitoringGraceEnforcementService>();
        builder.Services.AddHostedService<ErrorService.Server.Services.NotificationMaintenanceService>();
        builder.Services.AddHostedService<ErrorService.Server.Services.NotificationDispatcherService>();

// آمار بازدید — صف غیرمسدودکننده + نوشتن دسته‌ای + نگهداری/پاک‌سازی
builder.Services.AddSingleton<ErrorService.Server.Services.Visits.VisitTrackingService>();
builder.Services.AddHostedService<ErrorService.Server.Services.Visits.VisitLogWriterService>();
builder.Services.AddHostedService<ErrorService.Server.Services.Visits.VisitMaintenanceService>();

// تحلیلگر فضای هاست — اسکن پس‌زمینه با کش
builder.Services.AddSingleton<ErrorService.Server.Services.HostInfoService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ErrorService.Server.Services.HostInfoService>());
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
    // سفیدلیست origin: فقط دامنه سایت (Site:BaseUrl) + origin های تعریف‌شده در Cors:AllowedOrigins.
    // قبلاً SetIsOriginAllowed(_ => true) بود که هر origin دلخواهی را با credentials می‌پذیرفت
    // (امکان CSRF/سرقت توکن از سایت مهاجم). کلاینت Blazor same-origin است، پس محدودسازی بی‌اثر است.
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    static void AddOrigin(HashSet<string> set, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Uri.TryCreate(part, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                set.Add($"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : ":" + uri.Port)}");
        }
    }

    AddOrigin(allowed, builder.Configuration["Site:BaseUrl"]);
    AddOrigin(allowed, builder.Configuration["Cors:AllowedOrigins"]);
    if (builder.Environment.IsDevelopment())
    {
        // در توسعه، هر پورتی روی localhost/127.0.0.1 مجاز است تا پورت‌های
        // مختلف dotnet run / IIS Express / واتچ‌دگر مشکلی نداشته باشند.
        allowed.Add("https://localhost:7181");
        allowed.Add("http://localhost:5318");
    }

    var isDev = builder.Environment.IsDevelopment();
    options.AddPolicy("AllowBlazor", policy =>
    {
        if (allowed.Count == 0 && !isDev)
        {
            // اگر پیکربندی ناقص بود، هیچ origin خارجی مجاز نیست (بدون بازتاب origin).
            policy.SetIsOriginAllowed(_ => false)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else if (isDev)
        {
            policy.SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrEmpty(origin)) return false;
                        if (allowed.Contains(origin)) return true;
                        return Uri.TryCreate(origin, UriKind.Absolute, out var u)
                               && u.Host is "localhost" or "127.0.0.1" or "[::1]";
                    })
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(allowed.ToArray())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// محدودسازی نرخ درخواست — فقط روی endpoint های حساس با metadata فعال می‌شود
// (هیچ policy پیش‌فرض سراسری ندارد تا گشت‌وگذار عادی سایت هرگز محدود نشود).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        if (!response.HasStarted)
        {
            response.ContentType = "application/json; charset=utf-8";
            await response.WriteAsync("{\"error\":\"too_many_requests\",\"message\":\"تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی صبر کنید و دوباره تلاش کنید.\"}", cancellationToken);
        }
    };

    // ورود/ثبت‌نام: ۱۰ درخواست در هر ۱۵ ثانیه به ازای هر IP (ضد Brute-Force)
    options.AddPolicy("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(15),
            QueueLimit = 0
        });
    });

    // ثبت سفارش/کپن/دیدگاه: ۲۰ درخواست در ۱۰ ثانیه
    options.AddPolicy("commerce", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        });
    });

    // ردیابی بازدید (پرترافیک‌ترین نوشتن ناشناس): ۶۰ درخواست در ۱۰ ثانیه
    options.AddPolicy("tracking", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        });
    });

    // آپلود/پرداخت: ۸ درخواست در ۱۰ ثانیه
    options.AddPolicy("heavy", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        });
    });
});
builder.Services.AddScoped<BaleBotService>();
builder.Services.AddScoped<IChatMessengerChannel, BaleMessengerChannel>();
builder.Services.AddScoped<IChatMessengerChannel, TelegramMessengerChannel>();
builder.Services.AddScoped<IChatMessengerChannel, EitaaMessengerChannel>();
    builder.Services.AddScoped<MessengerRouter>();
    builder.Services.AddScoped<BaleSafirService>();
builder.Services.AddScoped<IChatAiService, FaqChatService>();
builder.Services.AddScoped<IChatAiService, OpenAiCompatibleChatService>();
builder.Services.AddScoped<ChatAiCoordinator>();
builder.Services.AddScoped<ChatAiResponder>();

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

    // Legacy public WebForms pages (still indexed by Google) -> new routes, permanent.
    // Only GET/HEAD so legacy form POSTs are never intercepted.
    if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
    {
        var legacyTarget = await MapLegacyWebFormsAsync(lower, context.Request.QueryString.Value ?? "", context.RequestServices);
        if (legacyTarget != null)
        {
            context.Response.Redirect(legacyTarget, permanent: true);
            return;
        }
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

// Cache policy for static assets, applied app-side so it does not depend on IIS
// <location> config (invalid clientCache attributes in web.config previously caused
// 500 on every /_framework/* and /uploads/* request in production):
//   /_framework hashed files and /uploads/* -> 1 year immutable;
//   stable-name Blazor runtime files (blazor.boot.json / blazor.webassembly.js /
//   dotnet.js + their .gz/.br variants) -> no-cache so releases take effect immediately;
//   /js/ and /css/ (un-versioned build output) -> no-cache so deploys are picked up
//   immediately instead of being served heuristically from the browser cache.
app.Use(async (context, next) =>
{
    var assetPath = context.Request.Path.Value ?? "";
    var isFramework = assetPath.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase);
    var isUploads = assetPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);
    var isVersionedSource = assetPath.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
        || assetPath.StartsWith("/css/", StringComparison.OrdinalIgnoreCase);
    // فایل‌هایی که باید هر بار revalidate شوند تا آپلود جدید فوراً اعمال شود:
    // service-worker (منبع سرویس مرورگر را کش می‌کند)، releases.json (جدول نسخه‌ها)
    // و index.html / همه پاسخ‌های HTML که SEOFallback تزریق می‌کند.
    var isAlwaysRevalidate = string.Equals(assetPath, "/service-worker.js", StringComparison.OrdinalIgnoreCase)
        || string.Equals(assetPath, "/releases.json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(assetPath, "/index.html", StringComparison.OrdinalIgnoreCase);
    if (isFramework || isUploads || isVersionedSource || isAlwaysRevalidate)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.HasStarted)
            {
                string cacheControl;
                if (isAlwaysRevalidate)
                {
                    // no-cache => مرورگر باید همیشه با سرور validate کند (ETag/304).
                    cacheControl = "no-cache, must-revalidate";
                }
                else if (isVersionedSource)
                {
                    // Un-versioned build output (js/css): always revalidate so a
                    // deploy is picked up immediately (ETag/304 keeps it cheap).
                    cacheControl = "no-cache";
                }
                else if (isFramework)
                {
                    var fileName = assetPath[(assetPath.LastIndexOf('/') + 1)..];
                    var isStableRuntime =
                        fileName.StartsWith("blazor.boot.json", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("blazor.webassembly.js", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("dotnet.js", StringComparison.OrdinalIgnoreCase);
                    cacheControl = isStableRuntime ? "no-cache" : "public, max-age=31536000, immutable";
                }
                else
                {
                    cacheControl = "public, max-age=31536000, immutable";
                }
                context.Response.Headers.CacheControl = cacheControl;
            }
            return Task.CompletedTask;
        });
    }
    await next();
});

// همه پاسخ‌های HTML (روت + همه مسیرهای SPA + تزریق SEO) نباید ذخیره شوند؛
// وگرنه کاربر بعد از دیپلوی مدتی نسخه قدیمی index.html را می‌بیند.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (!context.Response.HasStarted
            && context.Response.ContentType is not null
            && context.Response.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
            && !context.Response.Headers.ContainsKey("Cache-Control"))
        {
            context.Response.Headers.CacheControl = "no-cache, must-revalidate";
        }
        return Task.CompletedTask;
    });
    await next();
});

// فایل‌های PWA (سرویس‌ورکر/منیفست) و releases باید همیشه تازه باشند تا آپدیت اپ گیر نکند.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var p = context.Request.Path.Value ?? string.Empty;
        if (p.EndsWith("/service-worker.js", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/releases.json", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

// محدودسازی نرخ — بعد از UseRouting تا metadata مربوط به endpoint در دسترس باشد.
// فقط endpoint هایی که [EnableRateLimiting(...)] دارند محدود می‌شوند.
app.UseRateLimiter();

app.UseWebSockets();

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

        try
        {
            var ruleLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("NotificationRuleSeeder");
            await NotificationRuleSeeder.SeedAsync(db, ruleLogger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed notification rules.");
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

static async Task<string?> MapLegacyWebFormsAsync(string lowerPath, string query, IServiceProvider services)
{
    if (!lowerPath.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)) return null;

    const string viewPrefix = "/errorservice/formerror/view/";
    var idx = lowerPath.IndexOf(viewPrefix, StringComparison.Ordinal);
    if (idx < 0) return null;

    var page = lowerPath[(idx + viewPrefix.Length)..];

    switch (page)
    {
        case "index.aspx":
            return "/";
        case "news/news.aspx":
            return "/news";
        case "news/newsdetile.aspx":
            return await ResolveLegacyNewsAsync(query, services);
        case "counseling.aspx":
            return "/technical/consultation";
        case "learning.aspx":
            return "/academy";
        case "shop.aspx":
        case "shop/products.aspx":
            return "/products";
        case "shop/productdetile.aspx":
            return await ResolveLegacyProductAsync(query, services);
        case "pdetile.aspx":
            return "/trust/portfolio";
        case "setrequest.aspx":
            return LegacyQueryString(query, "action").Equals("kar", StringComparison.OrdinalIgnoreCase)
                ? "/admission/expertise"
                : "/admission/repair";
        case "dh.aspx":
        case "dhr.aspx":
            return "/contact";
        case "shop/cart.aspx":
            return "/cart";
        default:
            return null;
    }
}

static string LegacyQueryString(string query, string name)
{
    var m = System.Text.RegularExpressions.Regex.Match(
        query, @"(?:[?&]" + name + @"=)([^&]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    return m.Success ? Uri.UnescapeDataString(m.Groups[1].Value) : "";
}

static async Task<string> ResolveLegacyNewsAsync(string query, IServiceProvider services)
{
    if (int.TryParse(LegacyQueryString(query, "id"), out var id))
    {
        try
        {
            var db = services.GetRequiredService<ErrorServiceDbContext>();
            var slug = await db.News
                .Where(n => n.Id == id && n.IsPublished)
                .Select(n => n.Slug)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(slug)) return "/news/" + Uri.EscapeDataString(slug);
        }
        catch { /* fall through to hub */ }
    }
    return "/news";
}

static async Task<string> ResolveLegacyProductAsync(string query, IServiceProvider services)
{
    if (int.TryParse(LegacyQueryString(query, "id"), out var id))
    {
        try
        {
            var db = services.GetRequiredService<ErrorServiceDbContext>();
            var slug = await db.Products
                .Where(p => p.Id == id && p.IsAvailable)
                .Select(p => p.Slug)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(slug)) return "/products/" + Uri.EscapeDataString(slug);
        }
        catch { /* fall through to hub */ }
    }
    return "/products";
}

public sealed class SslSettingsHolder
{
    public bool EnableSsl { get; set; } = true;
    public int HstsMaxAgeDays { get; set; } = 7;
}
