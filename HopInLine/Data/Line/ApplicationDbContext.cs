using HopInLine.Data.Line;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public DbSet<Line> Lines { get; set; }
    public DbSet<Participant> Participants { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Participant>()
			.HasOne(p => p.Line)
			.WithMany(l => l.Participants)
			.HasForeignKey(p => p.LineId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}