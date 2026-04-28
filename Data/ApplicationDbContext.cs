using Microsoft.EntityFrameworkCore;
using GadgetVault.Models;

namespace GadgetVault.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<StockLevel> StockLevels { get; set; }

        // Phase 2 — Domain Models
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<WarehouseLocation> WarehouseLocations { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<BusinessPartner> BusinessPartners { get; set; }

        // Phase 3 — Purchase Orders
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Directly Seed the four pre-determined roles into the SSMS Roles Table
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "SystemManager", Description = "Super Admin: Full access to all modules" },
                new Role { Id = 2, Name = "WarehouseManager", Description = "Access to Inventory Valuation, Stock Adjustments, Reports" },
                new Role { Id = 3, Name = "WarehouseStaff", Description = "Limited to Pick-Pack-Ship and scanning items" },
                new Role { Id = 4, Name = "SalesAndProcurement", Description = "View stock, create Purchase/Sales Orders" },
                new Role { Id = 6, Name = "Vendor", Description = "External supplier access to POs" }
            );
        }
    }
}
