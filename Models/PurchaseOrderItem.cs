using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GadgetVault.Models
{
    public class PurchaseOrderItem
    {
        [Key]
        public int Id { get; set; }

        // ── Foreign Key → PurchaseOrder ───────────────────────────────────────
        [Required]
        public int PurchaseOrderId { get; set; }

        [ForeignKey(nameof(PurchaseOrderId))]
        public PurchaseOrder? PurchaseOrder { get; set; }

        // ── Foreign Key → Product ─────────────────────────────────────────────
        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        // ── Line Details ──────────────────────────────────────────────────────
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>Computed: Quantity × UnitPrice (not mapped to DB column).</summary>
        [NotMapped]
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
