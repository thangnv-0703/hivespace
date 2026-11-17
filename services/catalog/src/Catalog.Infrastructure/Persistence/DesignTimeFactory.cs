using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without a running database.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=design-time-only")
            .Options);
}
