using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GadgetVault.Models
{
    public enum SOStatus
    {
        Draft,
        Pending,
        Picking,
        Packed,
        Shipped,
        Cancelled
    }

    public class SalesOrder
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Auto-generated order reference, e.g. SO-20260428-001</summary>
        [Required]
        [StringLength(50)]
        public string SONumber { get; set; } = string.Empty;

        // ── Foreign Key → BusinessPartner (Customer) ─────────────────────────
        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public BusinessPartner? Customer { get; set; }

        // ── Order Details ─────────────────────────────────────────────────────
        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public SOStatus Status { get; set; } = SOStatus.Pending;

        [StringLength(500)]
        public string? Notes { get; set; }

        // ── Navigation ────────────────────────────────────────────────────────
        public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
    }
}
