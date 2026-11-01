using Microsoft.EntityFrameworkCore;
using MinooWebCore.Data.Models;

namespace MinooWebCore.Data;

/// <summary>
/// Maps the existing database schema. This context intentionally does not create or migrate the database.
/// </summary>
public sealed class MinooDbContext(DbContextOptions<MinooDbContext> options) : DbContext(options)
{
    public DbSet<UserModel> Users => Set<UserModel>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ReportParameterDefinition> ReportParameters => Set<ReportParameterDefinition>();
    public DbSet<SoftwareGroup> SoftwareGroups => Set<SoftwareGroup>();
    public DbSet<ReportLevel> ReportLevels => Set<ReportLevel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserModel>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.UsersKeyId);
        });
        modelBuilder.Entity<UserModel>().Ignore(p => p.VahedeTejariSn);

        modelBuilder.Entity<ReportDefinition>(entity =>
        {
            entity.ToTable("ReportDefinitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SourceType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ExcelFileName).HasMaxLength(260);
            entity.Property(x => x.ExcelContent).HasColumnType("varbinary(max)");
            entity.Property(x => x.SpecificServerVahedeTejariSN).HasPrecision(18, 3);
            entity.HasOne(x => x.SoftwareGroup).WithMany(x => x.Reports).HasForeignKey(x => x.SoftwareGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReportLevel).WithMany(x => x.Reports).HasForeignKey(x => x.ReportLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasIndex(x => x.Name)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_ReportDefinitions_ActiveName");
            entity.HasMany(x => x.Parameters)
                .WithOne(x => x.Report)
                .HasForeignKey(x => x.ReportDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<SoftwareGroup>(entity =>
        {
            entity.ToTable("SoftwareGroups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasData(
                new SoftwareGroup { Id = 1, Name = "مالی", IsActive = true },
                new SoftwareGroup { Id = 2, Name = "انبار و برنامه‌ریزی", IsActive = true },
                new SoftwareGroup { Id = 3, Name = "فروش و توزیع و پخش", IsActive = true });
        });

        modelBuilder.Entity<ReportLevel>(entity =>
        {
            entity.ToTable("ReportLevels");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasData(
                new ReportLevel { Id = 1, Name = "هلدینگ مینو", IsActive = true },
                new ReportLevel { Id = 2, Name = "شرکت قاسم ایران", IsActive = true });
        });

        modelBuilder.Entity<ReportParameterDefinition>(entity =>
        {
            entity.ToTable("ReportParameters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DisplayNameField).HasMaxLength(128);
            entity.Property(x => x.ValueField).HasMaxLength(128);
            entity.HasIndex(x => new { x.ReportDefinitionId, x.SortOrder })
                .HasDatabaseName("IX_ReportParameters_ReportDefinitionId");
        });
    }
}
