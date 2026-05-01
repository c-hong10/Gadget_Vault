using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GadgetVault.Models
{
    public enum TransactionType
    {
        StockIn,
        StockOut,
        Adjustment
    }

    public class InventoryTransaction
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ProductId { get; set; }
        
        [ForeignKey(nameof(ProductId))]
        public virtual Product? Product { get; set; }
        
        [Required]
        public int Quantity { get; set; }
        
        [Required]
        public TransactionType Type { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        [StringLength(100)]
        public string? ReferenceId { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        public int? LocationId { get; set; }
        
        [ForeignKey(nameof(LocationId))]
        public virtual WarehouseLocation? Location { get; set; }

        // Traceability: Link to Purchase Order
        public int? PurchaseOrderId { get; set; }
        
        [ForeignKey(nameof(PurchaseOrderId))]
        public virtual PurchaseOrder? PurchaseOrder { get; set; }
    }
}
