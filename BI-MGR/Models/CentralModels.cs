using System;
using Microsoft.EntityFrameworkCore;

namespace Bimasakti.BiService.Mgr.Models
{
    public class Company
    {
        public string CompanyId { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public string SyncConfigJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CentralDbContext : DbContext
    {
        private readonly string _databasePath;

        public CentralDbContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        public DbSet<Company> Companies => Set<Company>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_databasePath};Foreign Keys=True");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Company>(entity =>
            {
                entity.ToTable("companies");
                entity.HasKey(e => e.CompanyId);

                entity.Property(e => e.CompanyId).HasColumnName("company_id");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.SyncConfigJson).HasColumnName("sync_config_json");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });
        }
    }
}
