using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RealTimeTabSynchronizer.Server.Acknowledgments;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

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
		public DbSet<BrowserTab> BrowserTabs { get; set; }
		public DbSet<PendingRequest> PendingRequests { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.AddConfiguration(new TabDataMapping());
			modelBuilder.AddConfiguration(new ActiveTabMapping());
			modelBuilder.AddConfiguration(new BrowserMapping());
			modelBuilder.AddConfiguration(new BrowserTabMapping());
			modelBuilder.AddConfiguration(new PendingRequestMapping());
		}
	}
}