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
        // استفاده از connection string واقعی از appsettings.Development.json
        var connectionString = "Server=185.211.56.76,1463;Database=foodir_errorservice_new;User Id=foodir_errorservice;Password=M2223125!;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True";
        
        var options = new DbContextOptionsBuilder<ErrorServiceDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ErrorServiceDbContext(options);
    }
}
