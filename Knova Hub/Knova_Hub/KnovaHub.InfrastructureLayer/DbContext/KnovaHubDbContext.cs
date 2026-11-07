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
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<SupervisorDepartment> SupervisorDepartments => Set<SupervisorDepartment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Name).IsRequired().HasMaxLength(30);
            e.HasIndex(r => r.Name).IsUnique().HasDatabaseName("IX_Roles_Name");

            // Los cuatro roles del sistema. Solo existen por seed.
            e.HasData(
                new Role { Id = SystemRoles.AdminEmpresaRoleId, Name = SystemRoles.AdminEmpresaRoleName },
                new Role { Id = SystemRoles.SupervisorRoleId, Name = SystemRoles.SupervisorRoleName },
                new Role { Id = SystemRoles.UsuarioRoleId, Name = SystemRoles.UsuarioRoleName },
                new Role { Id = SystemRoles.AdminPlataformaRoleId, Name = SystemRoles.AdminPlataformaRoleName });
        });

        modelBuilder.Entity<Department>(e =>
        {
            e.ToTable("Departments", t =>
                t.HasCheckConstraint("CK_Departments_Name", "LEN(LTRIM(RTRIM([Name]))) > 0"));

            e.HasKey(d => d.Id);
            e.Property(d => d.Name).IsRequired().HasMaxLength(100);
            e.Property(d => d.IsActive).IsRequired();
            e.Property(d => d.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // El nombre no se repite dentro de la misma empresa.
            e.HasIndex(d => new { d.CompanyId, d.Name }).IsUnique().HasDatabaseName("IX_Departments_CompanyId_Name");

            e.HasOne(d => d.Company)
                .WithMany(c => c.Departments)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupervisorDepartment>(e =>
        {
            e.ToTable("SupervisorDepartments");
            e.HasKey(sd => new { sd.SupervisorId, sd.DepartmentId });

            e.HasOne(sd => sd.Supervisor)
                .WithMany(u => u.SupervisedDepartments)
                .HasForeignKey(sd => sd.SupervisorId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sd => sd.Department)
                .WithMany(d => d.Supervisors)
                .HasForeignKey(sd => sd.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
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
                // Solo el AdminPlataforma puede existir sin empresa.
                t.HasCheckConstraint("CK_Users_CompanyId_Role",
                    $"[CompanyId] IS NOT NULL OR [RoleId] = {SystemRoles.AdminPlataformaRoleId}");
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
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(u => u.Department)
                .WithMany(d => d.Members)
                .HasForeignKey(u => u.DepartmentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}