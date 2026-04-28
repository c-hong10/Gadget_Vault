using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GadgetVault.Models
{
    public class WarehouseLocation
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Zone { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Aisle { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Rack { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Bin { get; set; } = string.Empty;
    }
}
