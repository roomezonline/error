using ErrorService.Server.Models;
using ErrorService.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Data.Seed;

public static class AuthorizationSeeder
{
    public static async Task SeedAsync(ErrorServiceDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var bootstrapAdminPhone = (Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PHONE") ?? "").Trim();
        var bootstrapAdminPassword = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD") ?? "";

        var anyUser = await db.Users.AnyAsync(ct);
        if (!anyUser)
        {
            var user = new AppUser
            {
                FullName = "سوپر ادمین",
                PhoneNumber = bootstrapAdminPhone,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var hasher = new PasswordHasher<AppUser>();
            user.PasswordHash = hasher.HashPassword(user, bootstrapAdminPassword);

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("AuthorizationSeeder: Created initial AppUser (Id={UserId}) as super admin candidate.", user.Id);
        }

        var defs = PermissionRegistry.All;

        // Ensure all permissions from registry exist in database
        foreach (var def in defs)
        {
            var existing = await db.Permissions.FirstOrDefaultAsync(x => x.Key == def.Key, ct);
            if (existing == null)
            {
                db.Permissions.Add(new Models.Permission
                {
                    Key = def.Key,
                    TitleFa = def.TitleFa,
                    DescriptionFa = def.DescriptionFa,
                    GroupFa = def.GroupFa
                });
            }
            else
            {
                existing.TitleFa = def.TitleFa;
                existing.DescriptionFa = def.DescriptionFa;
                existing.GroupFa = def.GroupFa;
            }
        }

        await db.SaveChangesAsync(ct);

        // Remove permissions from database that are no longer in the registry
        var registryKeys = defs.Select(x => x.Key).ToList();
        var obsoletePermissions = await db.Permissions
            .Where(p => !registryKeys.Contains(p.Key))
            .ToListAsync(ct);

        if (obsoletePermissions.Any())
        {
            var obsoleteIds = obsoletePermissions.Select(x => x.Id).ToList();
            
            // Cleanup related role permissions
            var obsoleteRolePermissions = await db.RolePermissions
                .Where(rp => obsoleteIds.Contains(rp.PermissionId))
                .ToListAsync(ct);
            db.RolePermissions.RemoveRange(obsoleteRolePermissions);

            db.Permissions.RemoveRange(obsoletePermissions);
            await db.SaveChangesAsync(ct);
        }

        // Remove old system roles that are no longer needed
        var allowedSystemRoleKeys = new[] { "super_admin", "workshop_manager" };
        var oldSystemRoles = await db.Roles
            .Where(r => r.IsSystem && !allowedSystemRoleKeys.Contains(r.Key))
            .ToListAsync(ct);
        
        if (oldSystemRoles.Any())
        {
            // Remove role permissions for old roles
            var oldRoleIds = oldSystemRoles.Select(r => r.Id).ToList();
            var oldRolePermissions = await db.RolePermissions
                .Where(rp => oldRoleIds.Contains(rp.RoleId))
                .ToListAsync(ct);
            db.RolePermissions.RemoveRange(oldRolePermissions);
            
            // Remove user assignments for old roles
            var oldUserRoles = await db.AppUserRoles
                .Where(ur => oldRoleIds.Contains(ur.RoleId))
                .ToListAsync(ct);
            db.AppUserRoles.RemoveRange(oldUserRoles);
            
            // Remove the old roles
            db.Roles.RemoveRange(oldSystemRoles);
            await db.SaveChangesAsync(ct);
        }

        // Ensure only essential system roles exist
        var systemRoles = new[]
        {
            new { Key = "super_admin", TitleFa = "سوپر ادمین", DescriptionFa = "دسترسی کامل و بدون محدودیت به همه بخش‌های سایت، مدیریت کاربران و نقش‌ها، تنظیمات سیستم و گزارش‌گیری" },
            new { Key = "workshop_manager", TitleFa = "مدیر کارگاه", DescriptionFa = "مدیریت کامل کارگاه شامل کاربران کارگاه، رسیدها و حواله‌ها، انبار قطعات، تعمیرات و صدور مجوز برای کارکنان" }
        };

        foreach (var sysRole in systemRoles)
        {
            var rank = sysRole.Key switch
            {
                "super_admin" => 100,
                "workshop_manager" => 50,
                _ => 0
            };

            var role = await db.Roles.FirstOrDefaultAsync(x => x.Key == sysRole.Key, ct);
            if (role == null)
            {
                role = new Models.Role
                {
                    Key = sysRole.Key,
                    TitleFa = sysRole.TitleFa,
                    DescriptionFa = sysRole.DescriptionFa,
                    IsSystem = true,
                    Rank = rank
                };
                db.Roles.Add(role);
            }
            else
            {
                role.TitleFa = sysRole.TitleFa;
                role.DescriptionFa = sysRole.DescriptionFa;
                role.IsSystem = true;
                role.Rank = rank;
            }
        }
        await db.SaveChangesAsync(ct);

        // Ensure global VIP role exists (non-system)
        var vipRole = await db.Roles.FirstOrDefaultAsync(x => x.WorkshopId == null && x.Key == "vip", ct);
        if (vipRole == null)
        {
            db.Roles.Add(new Models.Role
            {
                Key = "vip",
                TitleFa = "کاربر VIP",
                DescriptionFa = "دسترسی ویژه به محتوای انحصاری سایت، تخفیف‌ها و خدمات VIP",
                IsSystem = false,
                Rank = 10
            });
            await db.SaveChangesAsync(ct);
        }
        else if (vipRole.Rank == 0)
        {
            vipRole.Rank = 10;
        }

        // Ensure common roles have descriptions
        var commonRoleDescriptions = new Dictionary<string, string>
        {
            { "vip", "دسترسی ویژه به محتوای انحصاری سایت، تخفیف‌ها و خدمات VIP" },
            { "workshop_staff", "دسترسی به بخش مدیریت کارگاه شامل ثبت حواله، صدور رسید، مدیریت قطعات و پیگیری وضعیت سفارش‌ها و درخواست‌ها" },
            { "technician", "دسترسی به بخش تعمیرات و سرویس‌های فنی، ثبت و بروزرسانی وضعیت تعمیرات، مدیریت قطعات یدکی و صدور گارانتی" },
            { "operator", "دسترسی به خط تولید و عملیات، نظارت بر فرآیندهای تولید، ثبت گزارش‌های عملیاتی و مدیریت وضعیت سفارش‌های تولید" },
            { "reception", "دسترسی به بخش پذیرش و نوبت‌دهی، ثبت درخواست‌های ورودی مشتریان، مدیریت نوبت‌ها و هماهنگی با واحد فنی" },
            { "accountant", "دسترسی به صورت‌حساب‌ها، مدیریت تراکنش‌های مالی، صدور فاکتور و پیش‌فاکتور، گزارش‌گیری حسابداری و بانک" },
            { "customer_service", "دسترسی به بخش پاسخگویی و تیکت‌های پشتیبانی، پیگیری درخواست‌های مشتریان، مدیریت ارتباط با مشتری (CRM)" },
            { "sales", "دسترسی به بخش فروش، مدیریت مشتریان و سرنخ‌ها، ثبت سفارش‌های فروش، صدور پیش‌فاکتور و پیگیری فرصت‌های فروش" },
            { "marketing", "دسترسی به بخش بازاریابی، مدیریت کمپین‌ها، پیام‌های تبلیغاتی، محتوای شبکه‌های اجتماعی و آنالیز بازاریابی" },
            { "content_manager", "دسترسی به بخش مدیریت محتوا شامل اخبار، مقالات، اسلایدرها، صفحات سایت و سئو" },
            { "support", "دسترسی به بخش پشتیبانی فنی، مدیریت تیکت‌ها، رفع مشکلات کاربران و مدیریت مستندات فنی" },
            { "admin", "دسترسی به تنظیمات کلی سیستم، مدیریت کاربران و نقش‌ها، مدیریت کارگاه‌ها و پیکربندی سایت" },
            { "user", "دسترسی پایه به سامانه شامل ثبت درخواست تعمیر و کارشناسی، مشاهده وضعیت درخواست‌ها و ویرایش پروفایل شخصی" }
        };

        foreach (var (key, desc) in commonRoleDescriptions)
        {
            var role = await db.Roles.FirstOrDefaultAsync(x => x.Key == key, ct);
            if (role != null && string.IsNullOrWhiteSpace(role.DescriptionFa))
            {
                role.DescriptionFa = desc;
            }
        }
        await db.SaveChangesAsync(ct);

        // Assign ALL permissions to super_admin dynamically
        var superRole = await db.Roles.FirstOrDefaultAsync(x => x.Key == "super_admin", ct);
        if (superRole != null)
        {
            var allPermIds = await db.Permissions.Select(x => x.Id).ToListAsync(ct);
            var existingPermIds = await db.RolePermissions
                .Where(x => x.RoleId == superRole.Id)
                .Select(x => x.PermissionId)
                .ToListAsync(ct);

            var missing = allPermIds.Except(existingPermIds).ToList();
            if (missing.Count > 0)
            {
                db.RolePermissions.AddRange(missing.Select(pid => new Models.RolePermission
                {
                    RoleId = superRole.Id,
                    PermissionId = pid
                }));
                await db.SaveChangesAsync(ct);
            }
        }

        var bootstrapUser = await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == bootstrapAdminPhone, ct);
        if (bootstrapUser != null && superRole != null)
        {
            var hasRole = await db.AppUserRoles.AnyAsync(x => x.UserId == bootstrapUser.Id && x.RoleId == superRole.Id, ct);
            if (!hasRole)
            {
                db.AppUserRoles.Add(new Models.AppUserRole { UserId = bootstrapUser.Id, RoleId = superRole.Id });
                await db.SaveChangesAsync(ct);
                logger.LogInformation("AuthorizationSeeder: Assigned super_admin role to bootstrap AppUser (Phone={Phone}, Id={UserId}).", bootstrapAdminPhone, bootstrapUser.Id);
            }
        }

        var anyUserRole = await db.AppUserRoles.AnyAsync(ct);
        if (!anyUserRole && superRole != null)
        {
            var firstUser = await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (firstUser != null)
            {
                db.AppUserRoles.Add(new Models.AppUserRole { UserId = firstUser.Id, RoleId = superRole.Id });
                await db.SaveChangesAsync(ct);
                logger.LogInformation("AuthorizationSeeder: Assigned super_admin role to first AppUser (Id={UserId}).", firstUser.Id);
            }
        }

        logger.LogInformation("AuthorizationSeeder: Seeded {Permissions} permissions and ensured super_admin role.", defs.Count);
    }
}
