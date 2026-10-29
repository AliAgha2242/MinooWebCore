using Microsoft.EntityFrameworkCore;

namespace MinooWebCore.Data;

/// <summary>
/// Central EF Core context. Add DbSet properties and model configuration as domain entities are introduced.
/// </summary>
public sealed class MinooDbContext(DbContextOptions<MinooDbContext> options) : DbContext(options)
{
}
