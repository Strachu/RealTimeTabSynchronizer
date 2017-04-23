using Microsoft.EntityFrameworkCore;

public class TabSynchronizerDbContext : DbContext
{
	public DbSet<TabData> Tabs { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		// TODO To config file
		optionsBuilder.UseNpgsql(@"Host=192.168.0.200;Database=realtimetabsynchronizer;Username=tabsynchronizer;Password=Test123;");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.AddConfiguration(new TabDataMapping());
	}
}