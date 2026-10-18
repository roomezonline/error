using System.Security.Claims;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.Messenger;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/stock-alerts")]
[Authorize]
public sealed class StockAlertsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly BaleBotService _baleBot;
    private readonly MessengerRouter _router;

    public StockAlertsController(ErrorServiceDbContext db, BaleBotService baleBot, MessengerRouter router)
    {
        _db = db;
        _baleBot = baleBot;
        _router = router;
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("user_id")
                  ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<StockAlertMineDto>> GetMine()
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
            return Ok(new StockAlertMineDto());

        var ids = await _db.StockAlerts
            .AsNoTracking()
            .Where(x => x.AppUserId == userId.Value && x.Status == StockAlertStatus.Active)
            .Select(x => x.ProductId)
            .ToListAsync();

        return Ok(new StockAlertMineDto { ActiveProductIds = ids });
    }

    [HttpPost("{productId:int}")]
    public async Task<IActionResult> Subscribe(int productId)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
            return Unauthorized("برای ثبت یادآوری باید وارد حساب کاربری شوید.");

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == productId);
        if (product == null)
            return NotFound("محصول یافت نشد.");

        if (product.IsAvailable && product.StockQuantity > 0)
            return BadRequest("این کالا هم‌اکنون موجود است.");

        var alert = await _db.StockAlerts
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value && x.ProductId == productId);

        if (alert == null)
        {
            _db.StockAlerts.Add(new StockAlert
            {
                AppUserId = userId.Value,
                ProductId = productId,
                Status = StockAlertStatus.Active
            });
        }
        else if (alert.Status != StockAlertStatus.Active)
        {
            alert.Status = StockAlertStatus.Active;
            alert.NotifiedAt = null;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique (AppUserId, ProductId) race — subscription already exists.
        }

        return Ok(new StockAlertMineDto { ActiveProductIds = await ActiveProductIdsAsync(userId.Value) });
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Unsubscribe(int productId)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var rows = await _db.StockAlerts
            .Where(x => x.AppUserId == userId.Value && x.ProductId == productId)
            .ToListAsync();

        if (rows.Count > 0)
        {
            _db.StockAlerts.RemoveRange(rows);
            await _db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpGet("connect-status")]
    public async Task<ActionResult<List<MessengerConnectChannelDto>>> GetConnectStatus()
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var userHasPhone = await _db.Users
            .Where(x => x.Id == userId.Value)
            .Select(x => x.PhoneNumber != null && x.PhoneNumber != "")
            .FirstOrDefaultAsync();

        var connectedChannels = await _db.MessengerEndpoints
            .Where(x => x.AppUserId == userId.Value && x.Status == MessengerEndpointStatus.Verified)
            .Select(x => x.Channel)
            .ToListAsync();
        var connectedSet = connectedChannels.ToHashSet();

        var result = new List<MessengerConnectChannelDto>();

        foreach (var channel in new[]
                 {
                     NotificationDeliveryChannel.Bale,
                     NotificationDeliveryChannel.Telegram,
                     NotificationDeliveryChannel.Eitaa
                 })
        {
            if (!MessengerChannels.IsEnabled(channel, settings))
                continue;

            var key = MessengerChannels.ChannelKey(channel);
            var title = MessengerChannels.ChannelTitle(channel);

            if (channel == NotificationDeliveryChannel.Bale)
            {
                // Safir delivers to the user's phone directly — no connection needed.
                var safirEnabled = !string.IsNullOrWhiteSpace(settings?.BaleSafirApiKey);
                if (safirEnabled && userHasPhone)
                    continue;
                if (connectedSet.Contains(NotificationDeliveryChannel.Bale))
                    continue;

                var username = await _baleBot.GetBotUsernameAsync();
                result.Add(new MessengerConnectChannelDto
                {
                    Key = key,
                    Title = title,
                    BotUrl = string.IsNullOrWhiteSpace(username) ? null : $"https://ble.ir/{username}",
                    SelfService = true,
                    Instruction =
                        "ربات بله را باز کنید، یک پیام کوتاه بفرستید و دکمهٔ «ارسال شمارهٔ من» را بزنید. " +
                        "شماره باید همان شماره‌ای باشد که با آن در سایت ثبت‌نام کرده‌اید."
                });
                continue;
            }

            if (connectedSet.Contains(channel))
                continue;

            var botChannel = _router.GetByKey(key);
            var botUsername = botChannel == null ? null : await botChannel.GetBotUsernameAsync();
            var botUrl = channel switch
            {
                NotificationDeliveryChannel.Telegram when !string.IsNullOrWhiteSpace(botUsername)
                    => $"https://t.me/{botUsername}",
                NotificationDeliveryChannel.Eitaa when !string.IsNullOrWhiteSpace(botUsername)
                    => $"https://eitaa.com/{botUsername}",
                _ => null
            };
            var verb = channel == NotificationDeliveryChannel.Telegram ? "تلگرام" : "ایتا";
            result.Add(new MessengerConnectChannelDto
            {
                Key = key,
                Title = title,
                BotUrl = botUrl,
                SelfService = false,
                Instruction =
                    $"ربات {verb} را باز و start کنید، سپس نام کاربری خود را برای پشتیبانی سایت بفرستید " +
                    "تا حساب شما برای دریافت اعلان متصل شود."
            });
        }

        return Ok(result);
    }

    private async Task<List<int>> ActiveProductIdsAsync(int userId) =>
        await _db.StockAlerts
            .AsNoTracking()
            .Where(x => x.AppUserId == userId && x.Status == StockAlertStatus.Active)
            .Select(x => x.ProductId)
            .ToListAsync();
}
