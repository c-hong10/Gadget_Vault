using System;

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
        public string? TwoFactorSecret { get; set; }
        
        // Link to BusinessPartner if the user is a Supplier
        public int? SupplierId { get; set; }
        public BusinessPartner? Supplier { get; set; }

        // Profile Details
        public string? ProfilePictureUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }

        // Session Info
        public DateTime? LastLoginAt { get; set; }
        public string? LastLoginIp { get; set; }

        // Security Tracking
        public int AccessFailedCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }

    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Permissions { get; set; } = string.Empty;
        
        public ICollection<User>? Users { get; set; }
    }
}
