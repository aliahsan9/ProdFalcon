using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExamDynamicsAPI.Infrastructure.Data
{
    public class ExamDynamicsDbContextFactory : IDesignTimeDbContextFactory<ExamDynamicsDbContext>
    {
        public ExamDynamicsDbContext CreateDbContext(string[] args)
        {
            // Build configuration to read appsettings.json
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ExamDynamicsDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            optionsBuilder.UseSqlServer(connectionString);

            return new ExamDynamicsDbContext(optionsBuilder.Options);
        }
    }
}
