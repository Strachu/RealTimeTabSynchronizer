using System;
using Microsoft.EntityFrameworkCore;

public class TabSynchronizerDbContext : DbContext
{
	public TabSynchronizerDbContext(DbContextOptions<TabSynchronizerDbContext> options)
		: base(options)
	{
	}

	public DbSet<TabData> Tabs { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.AddConfiguration(new TabDataMapping());
	}
}