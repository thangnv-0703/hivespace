using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without a running database.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=design-time-only")
            .UseOpenIddict()
            .Options);
}
