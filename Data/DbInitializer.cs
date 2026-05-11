using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GadgetVault.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context)
        {
            // Ensure Database is Created & Migrated
            await context.Database.MigrateAsync();

            // 1. Seed Roles
            if (!await context.Roles.AnyAsync())
            {
                var roles = new List<Role>
                {
                    new Role { Name = "Admin", Description = "Super Admin" },
                    new Role { Name = "WarehouseManager", Description = "Warehouse Manager" },
                    new Role { Name = "WarehouseStaff", Description = "Warehouse Staff" },
                    new Role { Name = "SalesAndProcurement", Description = "Sales and Procurement" },
                    new Role { Name = "Supplier", Description = "External Supplier" }
                };
                context.Roles.AddRange(roles);
                await context.SaveChangesAsync();
            }

            // 2. Ensure Default Supplier exists
            var globalTech = await context.BusinessPartners.FirstOrDefaultAsync(p => p.CompanyName == "Global Tech Inc");
            if (globalTech == null)
            {
                globalTech = new BusinessPartner { CompanyName = "Global Tech Inc", PartnerType = PartnerType.Supplier, IsActive = true };
                context.BusinessPartners.Add(globalTech);
                await context.SaveChangesAsync();
            }

            // 3. Seed Default Users
            if (!await context.Users.AnyAsync(u => u.Username == "alex.admin"))
            {
                var roles = await context.Roles.ToListAsync();
                var adminRole = roles.First(r => r.Name == "Admin").Id;
                var mgrRole = roles.First(r => r.Name == "WarehouseManager").Id;
                var staffRole = roles.First(r => r.Name == "WarehouseStaff").Id;
                var salesRole = roles.First(r => r.Name == "SalesAndProcurement").Id;
                var suppRole = roles.First(r => r.Name == "Supplier").Id;

                // Secure hashing using BCrypt
                var defaultPassword = "P@ssword123";
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

                context.Users.AddRange(
                    new User { Username = "alex.admin", FullName = "Alex Ray", Email = "admin@gadgetvault.com", PasswordHash = hashedPassword, RoleId = adminRole, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new User { Username = "jordan.mgr", FullName = "Jordan Chen", Email = "manager@gadgetvault.com", PasswordHash = hashedPassword, RoleId = mgrRole, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new User { Username = "casey.staff", FullName = "Casey Miller", Email = "staff@gadgetvault.com", PasswordHash = hashedPassword, RoleId = staffRole, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new User { Username = "morgan.sales", FullName = "Morgan Reed", Email = "sales@gadgetvault.com", PasswordHash = hashedPassword, RoleId = salesRole, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new User { Username = "global.supplier", FullName = "Global Tech Supplier", Email = "global.supplier@gmail.com", PasswordHash = hashedPassword, RoleId = suppRole, SupplierId = globalTech.Id, IsActive = true, CreatedAt = DateTime.UtcNow }
                );
                await context.SaveChangesAsync();
            }
            else
            {
                // MIGRATION: If users exist but have plain-text passwords, upgrade them
                var users = await context.Users.ToListAsync();
                bool upgraded = false;
                foreach (var user in users)
                {
                    if (!string.IsNullOrEmpty(user.PasswordHash) && !user.PasswordHash.StartsWith("$2"))
                    {
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
                        upgraded = true;
                    }
                }
                if (upgraded) await context.SaveChangesAsync();
            }

            // 4. Initial Audit Log
            if (!await context.AuditLogs.AnyAsync())
            {
                context.AuditLogs.Add(new AuditLog 
                { 
                    Action = "System Initialization", 
                    PerformedBy = "System", 
                    Details = "Default users and roles successfully seeded with secure BCrypt hashing." 
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
