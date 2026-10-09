using KnovaHub.DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Data;

public class KnovaHubDbContext : DbContext
{
    public KnovaHubDbContext(DbContextOptions<KnovaHubDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Name).IsRequired().HasMaxLength(30);
            e.HasIndex(r => r.Name).IsUnique().HasDatabaseName("IX_Roles_Name");

            e.HasData(
                new Role { Id = SystemRoles.AdminRoleId, Name = SystemRoles.AdminRoleName },
                new Role { Id = SystemRoles.EditorRoleId, Name = SystemRoles.EditorRoleName },
                new Role { Id = SystemRoles.UserRoleId, Name = SystemRoles.UserRoleName });
        });

        modelBuilder.Entity<Company>(e =>
        {
            // Validaciones a nivel de base de datos.
            e.ToTable("Companies", t =>
            {
                t.HasCheckConstraint("CK_Companies_Rnc",
                    "LEN([Rnc]) IN (9, 11) AND [Rnc] NOT LIKE '%[^0-9]%'");
                t.HasCheckConstraint("CK_Companies_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                t.HasCheckConstraint("CK_Companies_Email", "[Email] LIKE '%_@_%._%'");
                t.HasCheckConstraint("CK_Companies_Phone",
                    "LEN(LTRIM(RTRIM([Phone]))) >= 7 AND [Phone] NOT LIKE '%[^0-9+() -]%'");
                t.HasCheckConstraint("CK_Companies_Address", "LEN(LTRIM(RTRIM([Address]))) > 0");
            });

            e.HasKey(c => c.Id);
            e.Property(c => c.Rnc).IsRequired().HasMaxLength(11);
            e.Property(c => c.Name).IsRequired().HasMaxLength(150);
            e.Property(c => c.Email).IsRequired().HasMaxLength(150);
            e.Property(c => c.Phone).IsRequired().HasMaxLength(20);
            e.Property(c => c.Address).IsRequired().HasMaxLength(250);
            e.Property(c => c.IsActive).IsRequired();
            e.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasIndex(c => c.Rnc).IsUnique().HasDatabaseName("IX_Companies_Rnc");
            e.HasIndex(c => c.Email).IsUnique().HasDatabaseName("IX_Companies_Email");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", t =>
            {
                t.HasCheckConstraint("CK_Users_FullName", "LEN(LTRIM(RTRIM([FullName]))) > 0");
                t.HasCheckConstraint("CK_Users_Email", "[Email] LIKE '%_@_%._%'");
            });

            e.HasKey(u => u.Id);
            e.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            e.Property(u => u.Email).IsRequired().HasMaxLength(150);
            e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(100);
            e.Property(u => u.IsActive).IsRequired();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_Users_Email");

            e.HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}