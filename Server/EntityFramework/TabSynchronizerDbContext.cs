using System;
using Microsoft.EntityFrameworkCore;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server.EntityFramework
{
	public class TabSynchronizerDbContext : DbContext
	{
		public TabSynchronizerDbContext(DbContextOptions<TabSynchronizerDbContext> options)
			: base(options)
		{
		}

		public DbSet<TabData> Tabs { get; set; }
		public DbSet<ActiveTab> ActiveTab { get; set; }
		public DbSet<Browser> Browsers { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.AddConfiguration(new TabDataMapping());
			modelBuilder.AddConfiguration(new ActiveTabMapping());
			modelBuilder.AddConfiguration(new BrowserMapping());
		}
	}
}