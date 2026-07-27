using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ProdFalcon.Infrastructure.Tenancy;

namespace ProdFalcon.Infrastructure.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=.\\SQLEXPRESS;Database=ProdFalconDb;Trusted_Connection=True;TrustServerCertificate=True");

        return new ApplicationDbContext(optionsBuilder.Options, new NullTenantProvider());
    }
}
