using HopInLine.Data.Line;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public DbSet<Line> Lines { get; set; }
    public DbSet<Participant> Participants { get; set; }

    /// <summary>
    /// When true, saves keep Line.LastUpdated as-is. Used for automatic changes (background auto-advance)
    /// so LastUpdated reflects the last human activity, which drives idle-line cleanup.
    /// </summary>
    public bool PreserveLastUpdated { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Line>()
			.Property(l => l.Version)
			.IsConcurrencyToken();

		modelBuilder.Entity<Line>()
			.Property(l => l.AutoAdvanceInterval)
			.HasConversion(v => (int)v.TotalSeconds, v => TimeSpan.FromSeconds(v));

		modelBuilder.Entity<Line>()
			.Property(l => l.UnpauseRemaining)
			.HasConversion(v => v.HasValue ? (int?)v.Value.TotalSeconds : null, v => v.HasValue ? TimeSpan.FromSeconds(v.Value) : null);

		modelBuilder.Entity<Line>()
			.HasIndex(l => l.NextAdvanceAt);

		modelBuilder.Entity<Line>()
			.Property(l => l.Name)
			.HasMaxLength(100);

		modelBuilder.Entity<Line>()
			.Property(l => l.Description)
			.HasMaxLength(500);

		modelBuilder.Entity<Line>()
			.Property(l => l.ManageToken)
			.HasMaxLength(64);

		modelBuilder.Entity<Participant>()
			.Property(p => p.Name)
			.HasMaxLength(60);

		modelBuilder.Entity<Participant>()
			.Property(p => p.Description)
			.HasMaxLength(200);

		modelBuilder.Entity<Participant>()
			.Property(p => p.Color)
			.HasMaxLength(7);

		modelBuilder.Entity<Participant>()
			.Property(p => p.LeaveToken)
			.HasMaxLength(64);

		modelBuilder.Entity<Participant>()
			.HasOne(p => p.Line)
			.WithMany(l => l.Participants)
			.HasForeignKey(p => p.LineId)
			.OnDelete(DeleteBehavior.Cascade);
	}

	public override int SaveChanges()
	{
		UpdateLineEntries();
		return base.SaveChanges();
	}

	public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		UpdateLineEntries();
		return await base.SaveChangesAsync(cancellationToken);
	}

	private void UpdateLineEntries()
	{
		var entries = ChangeTracker.Entries<Line>()
			.Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
			.ToList();

		foreach (var entry in entries)
		{
			entry.Entity.Version = Guid.NewGuid();
			if (!PreserveLastUpdated)
			{
				entry.Entity.LastUpdated = DateTime.UtcNow;
			}
		}
	}
}
