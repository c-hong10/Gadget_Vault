using System;
using System.ComponentModel.DataAnnotations;

namespace GadgetVault.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Details { get; set; }

        [StringLength(50)]
        public string? EntityName { get; set; }

        public int? EntityId { get; set; }
    }
}
