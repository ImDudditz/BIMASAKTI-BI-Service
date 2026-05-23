using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace BimasaktiReports.FinancialReports.Backend.Engines
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Role { get; set; } = "";
        public string CompanyId { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    public class UserWidget
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string WidgetKey { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public int LayoutOrder { get; set; } = 0;
        public string? Config { get; set; } // Represented as serialized JSON string in SQLite
    }

    public class UserReport
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string ReportKey { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    public class TenantDbContext : DbContext
    {
        private readonly string _databasePath;

        public TenantDbContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<UserWidget> UserWidgets => Set<UserWidget>();
        public DbSet<UserReport> UserReports => Set<UserReport>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_databasePath};Foreign Keys=True");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.CompanyId);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Username).HasColumnName("username");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.CompanyId).HasColumnName("company_id");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
            });

            modelBuilder.Entity<UserWidget>(entity =>
            {
                entity.ToTable("user_widgets");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.WidgetKey);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.WidgetKey).HasColumnName("widget_key");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.LayoutOrder).HasColumnName("layout_order");
                entity.Property(e => e.Config).HasColumnName("config");
            });

            modelBuilder.Entity<UserReport>(entity =>
            {
                entity.ToTable("user_reports");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReportKey);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.ReportKey).HasColumnName("report_key");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
            });
        }
    }
}
