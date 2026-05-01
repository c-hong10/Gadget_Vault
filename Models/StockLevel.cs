using System;

namespace GadgetVault.Models
{
    public class StockLevel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public int? LocationId { get; set; }
        public virtual WarehouseLocation? Location { get; set; }
        public int Quantity { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
