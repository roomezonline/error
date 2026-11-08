using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ErrorService.Server.Data;

/// <summary>
/// فقط برای ابزارهای طراحی EF (migrations add)؛ هرگز در زمان اجرا استفاده نمی‌شود.
/// بدون این کلاس، ابزار EF هنگام ساخت مایگریشن به کانکشن استرینگ اپلیکیشن (تولید) دست می‌زند.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ErrorServiceDbContext>
{
    public ErrorServiceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ErrorServiceDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ErrorService_DesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ErrorServiceDbContext(options);
    }
}
