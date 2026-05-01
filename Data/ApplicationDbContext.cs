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
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        // Phase 4 — Sales Orders
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seeding will be handled via a dedicated service or migration script to avoid PK conflicts
        }
    }
}
