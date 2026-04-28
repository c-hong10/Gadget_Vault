namespace GadgetVault.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; } // Nullable because OAuth users might not have passwords
        
        // OAuth Integration
        public string? GoogleId { get; set; }
        public string? FacebookId { get; set; }

        // Navigation property replacing enum
        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public string FullName { get; set; } = "New User";
        public bool TwoFactorEnabled { get; set; } = false;
        
        // Link to BusinessPartner if the user is a Vendor
        public int? SupplierId { get; set; }
        public BusinessPartner? Supplier { get; set; }
    }

    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        public ICollection<User>? Users { get; set; }
    }
}
