using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Configurations;

internal static class OrganizationModelConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var organization = modelBuilder.Entity<Organization>();
        organization.ToTable("Organization", "Identity");
        organization.HasKey(x => x.OrganizationId).HasName("PkOrganization");
        organization.Property(x => x.RowVersion).IsRowVersion();

        var unit = modelBuilder.Entity<OrganizationUnit>();
        unit.ToTable("OrganizationUnit", "Identity");
        unit.HasKey(x => x.OrganizationUnitId).HasName("PkOrganizationUnit");
        unit.Property(x => x.UnitType).HasConversion<string>().HasMaxLength(20);
        unit.Property<OrganizationUnitType?>("ParentUnitType").HasConversion<string>().HasMaxLength(20)
            .HasComputedColumnSql("CONVERT(NVARCHAR(20), CASE [UnitType] WHEN N'Country' THEN N'Organization' WHEN N'Department' THEN N'Organization' WHEN N'Region' THEN N'Country' WHEN N'State' THEN N'Region' WHEN N'Branch' THEN N'State' WHEN N'Location' THEN N'Branch' WHEN N'Team' THEN N'Department' END)", stored: true);
        unit.Property(x => x.UnitCode).HasMaxLength(50);
        unit.Property(x => x.UnitName).HasMaxLength(200);
        unit.Property(x => x.Description).HasMaxLength(500);
        unit.Property<string>("NormalizedUnitCode").HasMaxLength(50)
            .HasComputedColumnSql("UPPER(LTRIM(RTRIM([UnitCode])))", stored: true);
        unit.Property(x => x.HierarchyPath).HasMaxLength(450);
        unit.Property(x => x.RowVersion).IsRowVersion();
        unit.Property(x => x.AddressLine1).HasMaxLength(250);
        unit.Property(x => x.AddressLine2).HasMaxLength(250);
        unit.Property(x => x.AddressLine3).HasMaxLength(250);
        unit.Property(x => x.City).HasMaxLength(100);
        unit.Property(x => x.District).HasMaxLength(100);
        unit.Property(x => x.StateName).HasMaxLength(100);
        unit.Property(x => x.PostalCode).HasMaxLength(20);
        unit.Property(x => x.CountryCode).HasMaxLength(3);
        unit.Property(x => x.Latitude).HasPrecision(9, 6);
        unit.Property(x => x.Longitude).HasPrecision(9, 6);
        unit.HasAlternateKey("OrganizationUnitId", "OrganizationId", "UnitType").HasName("UqOrganizationUnitOrganizationType");
        // The nullable-parent consistency FK is enforced by DbUp SQL, not an EF alternate key.
        // EF alternate keys cannot contain null; root units intentionally have no parent.
        unit.HasIndex("OrganizationUnitId", "OrganizationId", "ParentOrganizationUnitId").IsUnique()
            .HasDatabaseName("UqOrganizationUnitOrganizationParent");
        unit.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.NoAction);
        unit.HasOne<OrganizationUnit>().WithMany()
            .HasForeignKey("ParentOrganizationUnitId", "OrganizationId", "ParentUnitType")
            .HasPrincipalKey("OrganizationUnitId", "OrganizationId", "UnitType").OnDelete(DeleteBehavior.NoAction);
        unit.HasIndex("OrganizationId", "NormalizedUnitCode").IsUnique().HasDatabaseName("UqOrganizationUnitCode");
        unit.HasIndex(x => x.OrganizationId).IsUnique().HasFilter("[ParentOrganizationUnitId] IS NULL").HasDatabaseName("UqOrganizationUnitRoot");
        unit.HasIndex("OrganizationId", "HierarchyPath").IsUnique().HasFilter("[HierarchyPath] IS NOT NULL").HasDatabaseName("UqOrganizationUnitHierarchyPath");
        unit.HasIndex("OrganizationId", "ParentOrganizationUnitId").HasDatabaseName("IxOrganizationUnitParent");

        ConfigureNode<Country>(modelBuilder, "Country");

        ConfigureNode<Region>(modelBuilder, "Region");
        modelBuilder.Entity<Region>().HasOne(x => x.Country).WithMany()
            .HasForeignKey("CountryId", "OrganizationId").HasPrincipalKey("CountryId", "OrganizationId")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Region>().HasIndex("OrganizationId", "CountryId").HasDatabaseName("IxRegionCountry");

        ConfigureNode<State>(modelBuilder, "State");
        modelBuilder.Entity<State>().HasOne(x => x.Region).WithMany()
            .HasForeignKey("RegionId", "OrganizationId").HasPrincipalKey("RegionId", "OrganizationId")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<State>().HasIndex("OrganizationId", "RegionId").HasDatabaseName("IxStateRegion");

        ConfigureNode<Branch>(modelBuilder, "Branch");
        modelBuilder.Entity<Branch>().HasOne(x => x.State).WithMany()
            .HasForeignKey("StateId", "OrganizationId").HasPrincipalKey("StateId", "OrganizationId")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Branch>().HasIndex("OrganizationId", "StateId").HasDatabaseName("IxBranchState");

        ConfigureNode<Location>(modelBuilder, "Location");
        modelBuilder.Entity<Location>().HasOne<Branch>().WithMany()
            .HasForeignKey("BranchId", "OrganizationId").HasPrincipalKey("BranchId", "OrganizationId")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Location>().HasIndex("OrganizationId", "BranchId").HasDatabaseName("IxLocationBranch");

        ConfigureNode<Department>(modelBuilder, "Department");

        ConfigureNode<Team>(modelBuilder, "Team");
        modelBuilder.Entity<Team>().HasOne<Department>().WithMany()
            .HasForeignKey("DepartmentId", "OrganizationId").HasPrincipalKey("DepartmentId", "OrganizationId")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Team>().HasIndex("OrganizationId", "DepartmentId").HasDatabaseName("IxTeamDepartment");
    }

    private static void ConfigureNode<TEntity>(ModelBuilder modelBuilder, string typeName) where TEntity : class
    {
        var node = modelBuilder.Entity<TEntity>();
        node.ToTable(typeName, "Identity");
        node.HasKey(typeName + "Id").HasName("Pk" + typeName);
        node.Property<long>(typeName + "Id").ValueGeneratedNever();
        node.Property<long>("OrganizationUnitId").HasComputedColumnSql("[" + typeName + "Id]", stored: true);
        node.Property<OrganizationUnitType>("UnitType").HasConversion<string>().HasMaxLength(20);
        node.HasAlternateKey(typeName + "Id", "OrganizationId").HasName("Uq" + typeName + "Organization");
        node.HasOne<OrganizationUnit>("Unit").WithMany()
            .HasForeignKey(typeName + "Id", "OrganizationId", "UnitType")
            .HasPrincipalKey("OrganizationUnitId", "OrganizationId", "UnitType").OnDelete(DeleteBehavior.NoAction);
    }
}
