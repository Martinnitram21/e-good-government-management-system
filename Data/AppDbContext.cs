using GoodGovernanceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace GoodGovernanceApp.Data;

public class AppDbContext : DbContext
{
    // ── Core ──────────────────────────────────────────────────────────────────
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<ValidateUser> ValidateUsers { get; set; } = null!;
    public DbSet<AuditTrail> AuditTrails { get; set; } = null!;
    public DbSet<Office> Offices { get; set; } = null!;

    // ── Budget ────────────────────────────────────────────────────────────────
    public DbSet<MasterBudget> MasterBudgets { get; set; } = null!;
    public DbSet<BudgetAllocation> BudgetAllocations { get; set; } = null!;
    public DbSet<ProgramProvision> ProgramProvisions { get; set; } = null!;

    // ── Transactions & Caches ────────────────────────────────────────────────
    public DbSet<ConsolidatedTransactions> ConsolidatedTransactions { get; set; } = null!;
    public DbSet<TblTransaction> TblTransactions { get; set; } = null!;
    public DbSet<CrsBeneficiaryCache> CrsBeneficiaryCaches { get; set; } = null!;

    // ── People & Organization ─────────────────────────────────────────────────
    public DbSet<DepartmentRole> DepartmentRoles { get; set; } = null!;

    // ── Services & Requests ───────────────────────────────────────────────────
    public DbSet<TblService> TblServices { get; set; } = null!;

    // ── Files & Evaluations ──────────────────────────────────────────────────
    public DbSet<UploadedFile> UploadedFiles { get; set; } = null!;
    public DbSet<Evaluation> Evaluations { get; set; } = null!;

    // ── Legacy / Retained ────────────────────────────────────────────────────


    public DbSet<Parameter> Parameters { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Budget> Budgets { get; set; } = null!;

    public DbSet<SystemLog> SystemLogs { get; set; } = null!;
    public DbSet<ProjectDetail> ProjectDetails { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var updatedProp = entry.Entity.GetType().GetProperty("UpdatedAt");
            if (updatedProp != null && updatedProp.CanWrite && (updatedProp.PropertyType == typeof(DateTime) || updatedProp.PropertyType == typeof(DateTime?)))
            {
                updatedProp.SetValue(entry.Entity, DateTime.Now);
            }

            var updatedLowerProp = entry.Entity.GetType().GetProperty("updated_at");
            if (updatedLowerProp != null && updatedLowerProp.CanWrite && (updatedLowerProp.PropertyType == typeof(DateTime) || updatedLowerProp.PropertyType == typeof(DateTime?)))
            {
                updatedLowerProp.SetValue(entry.Entity, DateTime.Now);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Users ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Role).HasDefaultValue("user");
            entity.Property(u => u.Status).HasDefaultValue("active");
        });

        // ── Transaction ───────────────────────────────────────────────────────
        // Explicitly map only the 5 physical columns that exist in the DB.


        // ── ValidateUsers ─────────────────────────────────────────────────────
        modelBuilder.Entity<ValidateUser>(entity =>
        {
            entity.ToTable("validate_users");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Status).HasDefaultValue("pending");
            entity.HasOne(v => v.Category).WithMany().HasForeignKey(v => v.CategoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(v => v.AppUser).WithOne(u => u.ValidationInfo).HasForeignKey<ValidateUser>(v => v.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── AuditTrails ───────────────────────────────────────────────────────
        modelBuilder.Entity<AuditTrail>(entity =>
        {
            entity.ToTable("audit_trails");
            entity.HasKey(a => a.Id);
        });

        // ── Offices ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Office>(entity =>
        {
            entity.ToTable("tbl_offices");
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => o.OfficeCode).IsUnique();
        });

        // ── Master Budget ─────────────────────────────────────────────────────
        modelBuilder.Entity<MasterBudget>(entity =>
        {
            entity.ToTable("master_budget");
            entity.HasKey(m => m.Id);
            entity.HasOne(m => m.CreatedBy).WithMany().HasForeignKey(m => m.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Budget Allocations ────────────────────────────────────────────────
        modelBuilder.Entity<BudgetAllocation>(entity =>
        {
            entity.ToTable("officeallocations");
            entity.HasKey(b => b.Id);
            entity.HasOne(b => b.MasterBudget).WithMany(m => m.Allocations).HasForeignKey(b => b.MasterBudgetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(b => b.Office).WithMany().HasForeignKey(b => b.OfficeId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Program Provision ─────────────────────────────────────────────────
        modelBuilder.Entity<ProgramProvision>(entity =>
        {
            entity.ToTable("tbl_program_provision");
            entity.HasKey(p => p.Id);
            entity.HasOne(p => p.Office).WithMany().HasForeignKey(p => p.OfficeId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── Tbl Transaction ───────────────────────────────────────────────────
        modelBuilder.Entity<TblTransaction>(entity =>
        {
            entity.ToTable("tbl_transaction");
            entity.HasKey(t => t.Id);
            entity.HasOne(t => t.Program).WithMany().HasForeignKey(t => t.ProgramId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.BudgetAllocation).WithMany().HasForeignKey(t => t.BudgetAllocationId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.DistributedBy).WithMany().HasForeignKey(t => t.DistributedById).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.Office).WithMany().HasForeignKey(t => t.OfficeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.Service).WithMany().HasForeignKey(t => t.ServicesId).OnDelete(DeleteBehavior.SetNull);
        });





        // ── Services & Requests ───────────────────────────────────────────────
        modelBuilder.Entity<TblService>(entity => { entity.ToTable("tbl_services"); entity.HasKey(s => s.ServicesId); });


        // ── Project Details ───────────────────────────────────────────────────
        modelBuilder.Entity<ProjectDetail>(entity => { entity.ToTable("project_details"); entity.HasKey(p => p.Id); });

        // ── Legacy Retained Relations ─────────────────────────────────────────
        modelBuilder.Entity<Parameter>(entity =>
        {
            entity.ToTable("parameters");
            entity.Property(p => p.UpdatedAt).HasColumnName("UpdatedAt");
        });
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.Property(c => c.UpdatedAt).HasColumnName("UpdatedAt");
        });
        modelBuilder.Entity<Budget>().ToTable("budgets");
        modelBuilder.Entity<Budget>().HasOne(b => b.Category).WithMany(c => c.Budgets).HasForeignKey(b => b.CategoryId);
        modelBuilder.Entity<Budget>().HasOne(b => b.Office).WithMany().HasForeignKey(b => b.OfficeId).OnDelete(DeleteBehavior.SetNull);

        // The hosted MariaDB schema uses these established lowercase names.
        modelBuilder.Entity<DepartmentRole>().ToTable("departmentroles");
        modelBuilder.Entity<UploadedFile>().ToTable("uploadedfiles");
        modelBuilder.Entity<Evaluation>().ToTable("evaluations");
        modelBuilder.Entity<SystemLog>().ToTable("systemlogs");

        modelBuilder.Entity<SystemLog>().HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<User>().HasOne(u => u.Office).WithMany(o => o.Users).HasForeignKey(u => u.OfficeId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<DepartmentRole>().HasOne(dr => dr.Office).WithMany(o => o.Roles).HasForeignKey(dr => dr.OfficeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UploadedFile>().HasOne(f => f.Office).WithMany().HasForeignKey(f => f.OfficeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UploadedFile>().HasOne(f => f.Parameter).WithMany().HasForeignKey(f => f.ParameterId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Parameter>().HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Evaluation>().HasOne(e => e.UploadedFile).WithMany().HasForeignKey(e => e.UploadedFileId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Evaluation>().HasOne(e => e.Evaluator).WithMany().HasForeignKey(e => e.EvaluatorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql("Server=127.0.0.1;Port=3306;Database=governance;User=root;Password=root;SslMode=None;AllowPublicKeyRetrieval=True;", new MySqlServerVersion(new Version(8, 0, 31)));

        return new AppDbContext(optionsBuilder.Options);
    }
}
