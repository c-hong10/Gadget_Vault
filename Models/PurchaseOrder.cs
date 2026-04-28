using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GadgetVault.Models
{
    public enum POStatus
    {
        Draft,
        Ordered,
        Acknowledged,
        Received,
        Cancelled
    }

    public class PurchaseOrder
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Auto-generated order reference, e.g. PO-20260428-001</summary>
        [Required]
        [StringLength(50)]
        public string PONumber { get; set; } = string.Empty;

        // ── Foreign Key → BusinessPartner (Supplier) ─────────────────────────
        [Required]
        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public BusinessPartner? Supplier { get; set; }

        // ── Order Details ─────────────────────────────────────────────────────
        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public POStatus Status { get; set; } = POStatus.Draft;

        [StringLength(500)]
        public string? Notes { get; set; }

        // ── Navigation ────────────────────────────────────────────────────────
        public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    }
}
