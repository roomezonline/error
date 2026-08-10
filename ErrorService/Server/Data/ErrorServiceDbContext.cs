using ErrorService.Server.Models;
using ErrorService.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Data;

public sealed class ErrorServiceDbContext : DbContext
{
    public ErrorServiceDbContext(DbContextOptions<ErrorServiceDbContext> options) : base(options)
    {
    }

    public DbSet<SliderItem> SliderItems { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<News> News { get; set; }
    public DbSet<TrainingCourse> TrainingCourses { get; set; }
    public DbSet<TrainingLesson> TrainingLessons { get; set; }
    public DbSet<TrainingBlock> TrainingBlocks { get; set; }
    public DbSet<TrainingAttachment> TrainingAttachments { get; set; }
    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<TrainingArticle> TrainingArticles { get; set; }
    public DbSet<TrainingArticleBlock> TrainingArticleBlocks { get; set; }
    public DbSet<TrainingArticleComment> TrainingArticleComments { get; set; }
    public DbSet<SiteSettings> SiteSettings { get; set; }
    public DbSet<SiteHomeModuleSetting> SiteHomeModuleSettings { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<AppUser> Users { get; set; }
    public DbSet<ErrorCode> ErrorCodes { get; set; }
    public DbSet<ErrorCodeDocument> ErrorCodeDocuments { get; set; }
    public DbSet<ConsultationTicket> ConsultationTickets { get; set; }
    public DbSet<TicketReply> TicketReplies { get; set; }
    public DbSet<Testimonial> Testimonials { get; set; }
    public DbSet<PortfolioProject> PortfolioProjects { get; set; }
    public DbSet<FooterSettings> FooterSettings { get; set; }
    public DbSet<FooterLinkGroup> FooterLinkGroups { get; set; }
    public DbSet<FooterLink> FooterLinks { get; set; }
    public DbSet<SystemEventLog> SystemEventLogs { get; set; }
    public DbSet<ContactMessage> ContactMessages { get; set; }

    public DbSet<DeviceType> DeviceTypes { get; set; }
    public DbSet<DeviceBrand> DeviceBrands { get; set; }

    public DbSet<Province> Provinces { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Workshop> Workshops { get; set; }

    public DbSet<WorkshopCustomer> WorkshopCustomers { get; set; }
    public DbSet<CustomerReceipt> CustomerReceipts { get; set; }
    public DbSet<CustomerReceiptBilling> CustomerReceiptBillings { get; set; }
    public DbSet<PrintSettings> PrintSettings { get; set; }

    public DbSet<SensorFinderDevice> SensorFinderDevices { get; set; }
    public DbSet<SensorFinderRecord> SensorFinderRecords { get; set; }
    public DbSet<SensorFinderImage> SensorFinderImages { get; set; }

    public DbSet<Story> Stories { get; set; }
    public DbSet<StoryLike> StoryLikes { get; set; }

    public DbSet<PopularBrandItem> PopularBrandItems { get; set; }

    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<AppUserRole> AppUserRoles { get; set; }
    public DbSet<WorkshopUser> WorkshopUsers { get; set; }
    public DbSet<WorkshopUserRole> WorkshopUserRoles { get; set; }

    public DbSet<WorkshopInvoiceCatalogItem> WorkshopInvoiceCatalogItems { get; set; }

    public DbSet<MonitoringDevice> MonitoringDevices { get; set; }
    public DbSet<MonitoringDeviceAssignment> MonitoringDeviceAssignments { get; set; }
    public DbSet<MonitoringReceiptConnection> MonitoringReceiptConnections { get; set; }
    public DbSet<MonitoringDeviceChangeLog> MonitoringDeviceChangeLogs { get; set; }
    public DbSet<MonitoringDataRecord> MonitoringDataRecords { get; set; }
    public DbSet<MonitoringAlert> MonitoringAlerts { get; set; }
    public DbSet<MonitoringDataArchive> MonitoringDataArchives { get; set; }
    public DbSet<CachedCsvData> CachedCsvData { get; set; }
    public DbSet<MonitoringShareLink> MonitoringShareLinks { get; set; }
    public DbSet<ShareLinkViewerSession> ShareLinkViewerSessions { get; set; }
    public DbSet<MonitoringRenewalRequest> MonitoringRenewalRequests { get; set; }
    public DbSet<MonitoringPlan> MonitoringPlans { get; set; }

    public DbSet<OnlineAdmissionRequest> OnlineAdmissionRequests { get; set; }
    public DbSet<AdmissionExpertiseSetting> AdmissionExpertiseSettings { get; set; }

    public DbSet<NewsletterSubscription> NewsletterSubscriptions { get; set; }
    public DbSet<ProductReview> ProductReviews { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<BannedVisitor> BannedVisitors { get; set; }
    public DbSet<NewsComment> NewsComments { get; set; }

    public DbSet<SmsSettings> SmsSettings { get; set; }
    public DbSet<WorkshopSmsCredit> WorkshopSmsCredits { get; set; }
    public DbSet<WorkshopSmsModule> WorkshopSmsModules { get; set; }
    public DbSet<WorkshopSmsSetting> WorkshopSmsSettings { get; set; }
    public DbSet<SmsChargeTransaction> SmsChargeTransactions { get; set; }
    public DbSet<SmsLog> SmsLogs { get; set; }

    public DbSet<DigitalCommitment> DigitalCommitments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoringDevice>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DeviceNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(x => x.DeviceNumber).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<MonitoringDeviceAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.MonitoringDeviceId, x.WorkshopId });
            entity.HasIndex(x => x.StartAt);
            entity.HasIndex(x => x.EndAt);

            entity.HasOne(x => x.MonitoringDevice)
                .WithMany()
                .HasForeignKey(x => x.MonitoringDeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MonitoringPlan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.IsActive);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Price).HasColumnType("decimal(18,0)");
        });

        modelBuilder.Entity<MonitoringRenewalRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AssignmentId);
            entity.HasIndex(x => x.WorkshopId);
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.PaymentReceiptUrl).HasMaxLength(500);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.AdminNote).HasMaxLength(1000);
            entity.Property(x => x.PriceAtRequest).HasColumnType("decimal(18,0)");

            entity.HasOne(x => x.Assignment)
                .WithMany()
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MonitoringPlan)
                .WithMany()
                .HasForeignKey(x => x.MonitoringPlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MonitoringReceiptConnection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedByUserName).HasMaxLength(200);

            entity.HasIndex(x => x.WorkshopId);
            entity.HasIndex(x => x.CustomerReceiptId);
            entity.HasIndex(x => x.MonitoringDeviceId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.EndedAt);
            entity.HasIndex(x => new { x.CustomerReceiptId, x.EndedAt });

            entity.HasOne(x => x.CustomerReceipt)
                .WithMany(x => x.MonitoringReceiptConnections)
                .HasForeignKey(x => x.CustomerReceiptId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MonitoringDevice)
                .WithMany()
                .HasForeignKey(x => x.MonitoringDeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MonitoringDeviceChangeLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.ChangeType).IsRequired().HasMaxLength(50);
            entity.Property(x => x.OldValue).HasMaxLength(50);
            entity.Property(x => x.NewValue).HasMaxLength(50);
            entity.Property(x => x.ChangeDescription).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DataSnapshot).IsRequired();
            entity.Property(x => x.CreatedAtFa).HasMaxLength(30);

            entity.HasIndex(x => x.DeviceCode);
            entity.HasIndex(x => x.ChangeType);
            entity.HasIndex(x => x.MonitoringId);
            entity.HasIndex(x => new { x.DeviceCode, x.CreatedAt });
        });

        modelBuilder.Entity<MonitoringDataRecord>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DeviceCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.State).HasMaxLength(50);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.TimestampFa).HasMaxLength(30);

            entity.Property(x => x.TemperatureRef);
            entity.Property(x => x.TemperatureFreez);
            entity.Property(x => x.TemperatureEnv);
            entity.Property(x => x.Jaryan);
            entity.Property(x => x.Power);
            entity.Property(x => x.Kw);
            entity.Property(x => x.SumKw);

            entity.HasIndex(x => x.DeviceCode);
            entity.HasIndex(x => x.Timestamp);
            entity.HasIndex(x => new { x.DeviceCode, x.Timestamp });
            entity.HasIndex(x => x.MonitoringId);
            entity.HasIndex(x => x.CustomerReceiptId);

            entity.HasOne(x => x.CustomerReceipt)
                .WithMany()
                .HasForeignKey(x => x.CustomerReceiptId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MonitoringAlert>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Type).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Message).IsRequired().HasMaxLength(500);
            entity.Property(x => x.Command).IsRequired().HasMaxLength(50);

            entity.HasIndex(x => x.DeviceCode);
            entity.HasIndex(x => x.MonitoringId);
            entity.HasIndex(x => x.State);
            entity.HasIndex(x => x.Time);
            entity.HasIndex(x => new { x.DeviceCode, x.State });
            entity.HasIndex(x => new { x.DeviceCode, x.Time });
        });

        modelBuilder.Entity<MonitoringDataArchive>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceCode).IsRequired().HasMaxLength(50);

            entity.HasIndex(x => x.WorkshopId);
            entity.HasIndex(x => x.MonitoringReceiptConnectionId).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.WorkshopId, x.Downloaded });

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MonitoringReceiptConnection)
                .WithMany()
                .HasForeignKey(x => x.MonitoringReceiptConnectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CachedCsvData>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(100);
            entity.Property(x => x.Data).IsRequired();
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<MonitoringShareLink>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).IsRequired().HasMaxLength(64);
            entity.Property(x => x.DeviceCode).IsRequired().HasMaxLength(50);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => x.MonitoringId);
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<ShareLinkViewerSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.IpAddress).HasMaxLength(50);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.Property(x => x.DeviceInfo).HasMaxLength(200);
            entity.HasIndex(x => x.ShareLinkId);
            entity.HasIndex(x => x.SessionId).IsUnique();
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.LastPingAt);
        });

        modelBuilder.Entity<OnlineAdmissionRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.CustomerMobile);
            entity.HasIndex(x => x.AssignedWorkshopId);
            entity.HasIndex(x => x.CreatedReceiptId);

            entity.HasOne(x => x.DeviceType)
                .WithMany()
                .HasForeignKey(x => x.DeviceTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DeviceBrand)
                .WithMany()
                .HasForeignKey(x => x.DeviceBrandId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedWorkshop)
                .WithMany()
                .HasForeignKey(x => x.AssignedWorkshopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedReceipt)
                .WithMany()
                .HasForeignKey(x => x.CreatedReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkshopInvoiceCatalogItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DeviceType)
                .WithMany()
                .HasForeignKey(x => x.DeviceTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.WorkshopId);
            entity.HasIndex(x => new { x.WorkshopId, x.DeviceTypeId });
        });

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SliderItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Subtitle).HasMaxLength(400);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.Property(x => x.MobileImageUrl).HasMaxLength(500);
            entity.Property(x => x.LinkUrl).HasMaxLength(500);
            entity.HasIndex(x => new { x.IsActive, x.SortOrder });
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasMany(c => c.Products)
                  .WithOne(p => p.Category)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeviceType>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.SortOrder);

            entity.HasMany(x => x.Brands)
                .WithOne(x => x.DeviceType)
                .HasForeignKey(x => x.DeviceTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeviceBrand>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceTypeId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.DeviceTypeId, x.SortOrder });
        });

        modelBuilder.Entity<Province>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.HasIndex(x => new { x.ProvinceId, x.Name }).IsUnique();
            entity.HasOne(x => x.Province)
                .WithMany(x => x.Cities)
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Workshop>(entity =>
        {
            entity.HasOne(x => x.Province)
                .WithMany()
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.City)
                .WithMany()
                .HasForeignKey(x => x.CityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.MobileNumber);
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<OnlineAdmissionRequest>(entity =>
        {
            entity.HasOne(x => x.Province)
                .WithMany()
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.City)
                .WithMany()
                .HasForeignKey(x => x.CityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkshopCustomer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Mobile).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Address).HasMaxLength(1000);

            entity.HasIndex(x => new { x.WorkshopId, x.Mobile }).IsUnique();
            entity.HasIndex(x => new { x.WorkshopId, x.CreatedAt });

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProblemDescription).IsRequired().HasMaxLength(3000);
            entity.Property(x => x.ReceiptImageUrl).HasMaxLength(500);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(50);
            entity.HasIndex(x => new { x.WorkshopId, x.RegisteredAt });

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Receipts)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DeviceType)
                .WithMany()
                .HasForeignKey(x => x.DeviceTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DeviceBrand)
                .WithMany()
                .HasForeignKey(x => x.DeviceBrandId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DigitalCommitment)
                .WithOne(x => x.CustomerReceipt)
                .HasForeignKey<DigitalCommitment>(x => x.CustomerReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerReceiptBilling>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CustomerReceiptId).IsUnique();

            entity.HasOne(x => x.CustomerReceipt)
                .WithOne(x => x.Billing)
                .HasForeignKey<CustomerReceiptBilling>(x => x.CustomerReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerReceiptInvoiceItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ActionDescription).HasMaxLength(1000);

            entity.HasOne(x => x.Billing)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.CustomerReceiptBillingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Technician)
                .WithMany()
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DigitalCommitment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomerFullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.CustomerMobile).IsRequired().HasMaxLength(20);
            entity.Property(x => x.DeviceTypeName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DeviceBrandName).HasMaxLength(200);
            entity.Property(x => x.RegisteredAtFa).IsRequired().HasMaxLength(20);
            entity.Property(x => x.WorkshopName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.WorkshopLogoUrl).HasMaxLength(500);
            entity.Property(x => x.ExtraBodyText).HasMaxLength(2000);
            entity.Property(x => x.FullBodyText).IsRequired().HasMaxLength(4000);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(50);
            entity.Property(x => x.SenderLineNumber).HasMaxLength(50);
            entity.Property(x => x.VerificationCode).HasMaxLength(20);

            entity.HasIndex(x => x.CustomerMobile);

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SensorFinderDevice>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<SensorFinderRecord>(entity =>
        {
            entity.HasOne(x => x.Device)
                .WithMany(x => x.Records)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DeviceId, x.SensorName });
        });

        modelBuilder.Entity<SensorFinderImage>(entity =>
        {
            entity.HasOne(x => x.Record)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.RecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.RecordId, x.SortOrder });
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasIndex(x => new { x.IsActive, x.CreatedAt });
        });

        modelBuilder.Entity<StoryLike>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IpAddress).IsRequired().HasMaxLength(50);
            entity.HasIndex(x => new { x.StoryId, x.IpAddress }).IsUnique();
            entity.HasOne(x => x.Story)
                .WithMany(x => x.Likes)
                .HasForeignKey(x => x.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PopularBrandItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.Property(x => x.LinkUrl).HasMaxLength(500);
            entity.HasIndex(x => new { x.IsActive, x.SortOrder });
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired().HasMaxLength(150);
            entity.Property(x => x.TitleFa).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DescriptionFa).HasMaxLength(500);
            entity.Property(x => x.GroupFa).HasMaxLength(200);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired().HasMaxLength(100);
            entity.Property(x => x.TitleFa).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DescriptionFa).HasMaxLength(500);
            entity.HasIndex(x => new { x.WorkshopId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Permission)
                .WithMany()
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUserRole>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkshopUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            entity.HasIndex(x => new { x.WorkshopId, x.PhoneNumber }).IsUnique();
            entity.HasIndex(x => new { x.WorkshopId, x.IsActive });

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkshopUserRole>(entity =>
        {
            entity.HasKey(x => new { x.WorkshopUserId, x.RoleId });
            entity.HasOne(x => x.WorkshopUser)
                .WithMany()
                .HasForeignKey(x => x.WorkshopUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => new { x.ProductId, x.IsApproved });
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.IsAvailable);
            entity.HasIndex(p => p.CreatedAt);
            entity.HasIndex(p => p.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasIndex(p => p.IsPublished);
            entity.HasIndex(p => p.CreatedAt);
            entity.HasIndex(p => p.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
        });

        modelBuilder.Entity<TrainingCourse>(entity =>
        {
            entity.HasIndex(x => new { x.IsPublished, x.SortOrder });
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
        });

        modelBuilder.Entity<TrainingLesson>(entity =>
        {
            entity.HasIndex(x => new { x.CourseId, x.IsPublished, x.SortOrder });
            entity.HasIndex(x => x.CreatedAt);

            entity.HasOne(x => x.Course)
                .WithMany(x => x.Lessons)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingBlock>(entity =>
        {
            entity.HasIndex(x => new { x.LessonId, x.SortOrder });
            entity.HasIndex(x => x.BlockType);

            entity.HasOne(x => x.Lesson)
                .WithMany(x => x.Blocks)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingAttachment>(entity =>
        {
            entity.HasIndex(x => new { x.LessonId, x.SortOrder });

            entity.HasOne(x => x.Lesson)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Role).HasMaxLength(200);
            entity.Property(x => x.Bio).HasMaxLength(1000);
            entity.Property(x => x.PhotoUrl).HasMaxLength(500);
            entity.HasIndex(x => new { x.IsPublished, x.SortOrder });
        });

        modelBuilder.Entity<TrainingArticle>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(300);
            entity.Property(x => x.Summary).HasMaxLength(1000);
            entity.Property(x => x.CoverImageUrl).HasMaxLength(500);
            entity.HasIndex(x => new { x.IsPublished, x.CreatedAt });
            entity.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
        });

        modelBuilder.Entity<TrainingArticleBlock>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ArticleId, x.SortOrder });
            entity.HasOne(x => x.Article)
                .WithMany(x => x.Blocks)
                .HasForeignKey(x => x.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingArticleComment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Content).IsRequired().HasMaxLength(2000);
            entity.HasIndex(x => new { x.ArticleId, x.IsApproved });
            entity.HasOne(x => x.Article)
                .WithMany()
                .HasForeignKey(x => x.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NewsComment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Content).IsRequired().HasMaxLength(2000);
            entity.HasIndex(x => new { x.NewsId, x.IsApproved });
            entity.HasOne(x => x.News)
                .WithMany()
                .HasForeignKey(x => x.NewsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SiteSettings>(entity =>
        {
            entity.HasMany(x => x.HomeModules)
                .WithOne()
                .HasForeignKey(x => x.SiteSettingsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SiteHomeModuleSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SiteSettingsId, x.SortOrder });
            entity.HasIndex(x => new { x.SiteSettingsId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.IsActive, x.ShowInGateway, x.SortOrder });
            entity.HasIndex(x => new { x.WorkshopId, x.IsActive });
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.BankName).HasMaxLength(200);
            entity.Property(x => x.OwnerName).HasMaxLength(200);
            entity.Property(x => x.CardNumber).HasMaxLength(32);
            entity.Property(x => x.AccountNumber).HasMaxLength(64);
            entity.Property(x => x.Iban).HasMaxLength(64);
            entity.Property(x => x.IconUrl).HasMaxLength(500);

            entity.HasOne<Workshop>()
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<ErrorCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Brand).IsRequired().HasMaxLength(100);
            entity.Property(x => x.DeviceType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.HasIndex(x => new { x.Brand, x.DeviceType, x.Code });

            entity.HasMany(x => x.Documents)
                .WithOne()
                .HasForeignKey(x => x.ErrorCodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ErrorCodeDocument>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Url).IsRequired().HasMaxLength(1000);
            entity.HasIndex(x => new { x.ErrorCodeId, x.SortOrder });
        });

        modelBuilder.Entity<ConsultationTicket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.HasMany(x => x.Replies)
                .WithOne()
                .HasForeignKey(x => x.ConsultationTicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FooterSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.CompanyDescription).HasMaxLength(500);
            entity.Property(x => x.LogoUrl).HasMaxLength(500);
            entity.Property(x => x.PhoneNumber1).HasMaxLength(20);
            entity.Property(x => x.PhoneNumber2).HasMaxLength(20);
            entity.Property(x => x.WhatsAppNumber).HasMaxLength(20);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.InstagramUrl).HasMaxLength(500);
            entity.Property(x => x.TelegramUrl).HasMaxLength(500);
            entity.Property(x => x.YouTubeUrl).HasMaxLength(500);
            entity.Property(x => x.LinkedInUrl).HasMaxLength(500);
            entity.Property(x => x.EnamadCode).HasMaxLength(50);
            entity.Property(x => x.SamandehiCode).HasMaxLength(50);
            entity.Property(x => x.EtaCode).HasMaxLength(50);
            entity.Property(x => x.CopyrightText).HasMaxLength(200);
            entity.HasIndex(x => x.IsActive);
            entity.HasMany(x => x.LinkGroups)
                .WithOne()
                .HasForeignKey(x => x.FooterSettingsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FooterLinkGroup>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(100);
            entity.HasIndex(x => new { x.FooterSettingsId, x.SortOrder });
            entity.HasMany(x => x.Links)
                .WithOne()
                .HasForeignKey(x => x.FooterLinkGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FooterLink>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Url).IsRequired().HasMaxLength(500);
            entity.Property(x => x.Icon).HasMaxLength(50);
            entity.HasIndex(x => new { x.FooterLinkGroupId, x.SortOrder });
        });

        // SystemEventLog Configuration - چشمانی قوی مانیتورینگ
        modelBuilder.Entity<SystemEventLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            
            entity.Property(x => x.PersianTitle).HasMaxLength(200);
            entity.Property(x => x.TechnicalTitle).HasMaxLength(200);
            entity.Property(x => x.ErrorCode).HasMaxLength(100);
            entity.Property(x => x.ClientIp).HasMaxLength(50);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.Property(x => x.UserName).HasMaxLength(100);
            entity.Property(x => x.RequestPath).HasMaxLength(500);
            entity.Property(x => x.HttpMethod).HasMaxLength(10);
            entity.Property(x => x.Component).HasMaxLength(100);
            entity.Property(x => x.DeviceId).HasMaxLength(100);
            entity.Property(x => x.DeviceType).HasMaxLength(20);
            
            // Indexes for performance
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => x.Severity);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.IsResolved);
            entity.HasIndex(x => new { x.Severity, x.OccurredAt });
            entity.HasIndex(x => new { x.Category, x.OccurredAt });
            entity.HasIndex(x => new { x.UserId, x.OccurredAt });
            entity.HasIndex(x => x.ErrorCode);
            entity.HasIndex(x => new { x.IsResolved, x.Severity });
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VisitorId).IsRequired().HasMaxLength(100);
            entity.Property(x => x.UserName).HasMaxLength(200);
            entity.Property(x => x.UserEmail).HasMaxLength(200);
            entity.HasIndex(x => x.VisitorId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });

            entity.HasOne(x => x.Operator)
                .WithMany()
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(x => x.Messages)
                .WithOne(x => x.Session)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(4000);
            entity.Property(x => x.MediaUrl).HasMaxLength(1000);
            entity.Property(x => x.SenderId).HasMaxLength(100);
            entity.HasIndex(x => x.SessionId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.SessionId, x.CreatedAt });
        });

        modelBuilder.Entity<SmsSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApiKey).HasMaxLength(500);
            entity.Property(x => x.SenderNumber).HasMaxLength(50);
            entity.Property(x => x.TariffPerSms).HasPrecision(18, 0);
        });

        modelBuilder.Entity<WorkshopSmsCredit>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.WorkshopId).IsUnique();
            entity.Property(x => x.Balance).HasPrecision(18, 0);
            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkshopSmsModule>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.WorkshopId, x.ModuleType }).IsUnique();
            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkshopSmsSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.WorkshopId).IsUnique();
            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SmsChargeTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 0);
            entity.HasIndex(x => new { x.WorkshopId, x.Status });
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SmsLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RecipientNumber).IsRequired().HasMaxLength(20);
            entity.Property(x => x.MessageText).IsRequired();
            entity.Property(x => x.ProviderMessageId).HasMaxLength(100);
            entity.Property(x => x.Cost).HasPrecision(18, 0);
            entity.HasIndex(x => new { x.WorkshopId, x.CreatedAt });
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
