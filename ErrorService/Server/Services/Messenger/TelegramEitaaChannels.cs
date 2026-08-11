using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Messenger;

public sealed class TelegramMessengerChannel : MessengerHttpChannelBase
{
    private readonly ErrorServiceDbContext _db;

    public TelegramMessengerChannel(IHttpClientFactory httpClientFactory, ErrorServiceDbContext db, ILogger<TelegramMessengerChannel> logger)
        : base(httpClientFactory, logger)
    {
        _db = db;
    }

    public override string Key => "telegram";

    public override string DisplayName => "تلگرام";

    protected override string ApiBaseUrl => "https://api.telegram.org";

    protected override string FileBaseUrl => "https://api.telegram.org";

    protected override string? GetToken(SiteSettings settings) => settings.TelegramBotToken;

    protected override string? GetGroupId(SiteSettings settings) => settings.TelegramGroupId;

    public override bool IsEnabled(SiteSettings settings) => settings.EnableOnlineChat && settings.EnableTelegramChat;

    protected override async Task<SiteSettings> LoadSettingsCoreAsync()
    {
        try
        {
            return await _db.SiteSettings.FirstOrDefaultAsync() ?? new SiteSettings();
        }
        catch
        {
            return new SiteSettings();
        }
    }
}

public sealed class EitaaMessengerChannel : MessengerHttpChannelBase
{
    private readonly ErrorServiceDbContext _db;

    public EitaaMessengerChannel(IHttpClientFactory httpClientFactory, ErrorServiceDbContext db, ILogger<EitaaMessengerChannel> logger)
        : base(httpClientFactory, logger)
    {
        _db = db;
    }

    public override string Key => "eitaa";

    public override string DisplayName => "ایتا";

    protected override string ApiBaseUrl => "https://eitaayar.ir/api";

    protected override string FileBaseUrl => "https://eitaayar.ir";

    protected override string? GetToken(SiteSettings settings) => settings.EitaaBotToken;

    protected override string? GetGroupId(SiteSettings settings) => settings.EitaaGroupId;

    public override bool IsEnabled(SiteSettings settings) => settings.EnableOnlineChat && settings.EnableEitaaChat;

    protected override async Task<SiteSettings> LoadSettingsCoreAsync()
    {
        try
        {
            return await _db.SiteSettings.FirstOrDefaultAsync() ?? new SiteSettings();
        }
        catch
        {
            return new SiteSettings();
        }
    }
}