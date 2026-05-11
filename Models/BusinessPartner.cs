using System.ComponentModel.DataAnnotations;

namespace GadgetVault.Models
{
    public enum PartnerType
    {
        Supplier,
        Customer
    }

    public class BusinessPartner
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone Number must be exactly 11 digits.")]
        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public PartnerType PartnerType { get; set; }

        /// <summary>False = archived (soft-deleted). Defaults to true for all new partners.</summary>
        public bool IsActive { get; set; } = true;
    }
}
