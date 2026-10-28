using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public static class MonitoringConnectionLifecycle
{
    public static async Task CloseAndArchiveAsync(
        ErrorServiceDbContext db,
        MonitoringReceiptConnection connection,
        string deviceCode,
        string endReason)
    {
        if (connection.EndedAt != null)
            return;

        var now = DateTimeOffset.UtcNow;

        // بستن اتمی: اگر کنترلر و سرویس پس‌زمینه هم‌زمان اقدام کنند، فقط یکی موفق می‌شود.
        var affected = await db.MonitoringReceiptConnections
            .Where(c => c.Id == connection.Id && c.EndedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.EndedAt, now)
                .SetProperty(c => c.EndReason, endReason));

        if (affected == 0)
            return;

        connection.EndedAt = now;
        connection.EndReason = endReason;

        var recordCount = await db.MonitoringDataRecords
            .CountAsync(r => r.MonitoringId == connection.Id);

        db.MonitoringDataArchives.Add(new MonitoringDataArchive
        {
            WorkshopId = connection.WorkshopId,
            MonitoringReceiptConnectionId = connection.Id,
            CustomerReceiptId = connection.CustomerReceiptId,
            DeviceCode = deviceCode,
            StartedAt = connection.CreatedAt.DateTime,
            EndedAt = DateTime.UtcNow,
            RecordCount = recordCount,
            EstimatedBytes = recordCount * 300L,
            Downloaded = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
