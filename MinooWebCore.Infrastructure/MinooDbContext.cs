using Microsoft.EntityFrameworkCore;
using MinooWebCore.Data.Models;

namespace MinooWebCore.Data;

/// <summary>
/// Maps the existing database schema. This context intentionally does not create or migrate the database.
/// </summary>
public sealed class MinooDbContext(DbContextOptions<MinooDbContext> options) : DbContext(options)
{
    public DbSet<UserModel> Users => Set<UserModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserModel>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.UsersKeyId);
        });
        modelBuilder.Entity<UserModel>().Ignore(p => p.VahedeTejariSn);
    }
}