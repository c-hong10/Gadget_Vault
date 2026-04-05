namespace GadgetVault.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int ReorderThreshold { get; set; }
        public string? Location { get; set; } // E.g. Aisle-1/Rack-2/Bin-3
    }
}
