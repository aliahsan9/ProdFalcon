using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExamDynamicsAPI.Infrastructure.Seeders 
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(
            ExamDynamicsDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            await context.Database.MigrateAsync();

            var rand = new Random();

            // ==================== Roles ====================
            var roles = new[] { "Admin"};
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new ApplicationRole
                    {
                        Name = roleName,
                        Description = $"{roleName} role"
                    });
                }
            }

            // ==================== Users ====================
            var adminUser = await userManager.FindByEmailAsync("admin@example.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    FullName = "Admin User",
                    Email = "admin@example.com",
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(adminUser, "Admin@123");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            
            // ==================== Exams ====================
            if (!context.Exams.Any())
            {
                var exams = new List<Exam>
                {
                    new Exam { Title = "Math Exam", Description = "Basic math test", CreatedAt = DateTime.UtcNow },
                    new Exam { Title = "Science Exam", Description = "Basic science test", CreatedAt = DateTime.UtcNow },
                    new Exam { Title = "English Exam", Description = "English grammar test", CreatedAt = DateTime.UtcNow }
                };
                context.Exams.AddRange(exams);
                await context.SaveChangesAsync();
            }

            // ==================== Exam Categories ====================
            if (!context.ExamCategories.Any())
            {
                foreach (var exam in context.Exams.ToList())
                {
                    context.ExamCategories.Add(new ExamCategory
                    {
                        Name = $"{exam.Title} Category",
                        ExamId = exam.ExamId
                    });
                }
                await context.SaveChangesAsync();
            }  
        }
    }
}