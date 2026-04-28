using System.ComponentModel.DataAnnotations;

namespace GadgetVault.Models
{
    public class SystemSettings
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(7)]
        public string? PrimaryColorHex { get; set; }
    }
}
