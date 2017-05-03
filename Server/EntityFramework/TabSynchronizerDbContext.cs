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
		private readonly IModelBuildingService mModelBuildingService;

		public TabSynchronizerDbContext(
			DbContextOptions<TabSynchronizerDbContext> options,
			IModelBuildingService modelBuildingService)
			: base(options)
		{
			mModelBuildingService = modelBuildingService;
		}

		public DbSet<TabData> Tabs { get; set; }
		public DbSet<ActiveTab> ActiveTab { get; set; }
		public DbSet<Browser> Browsers { get; set; }
		public DbSet<BrowserTab> BrowserTabs { get; set; }
		public DbSet<PendingRequest> PendingRequests { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			mModelBuildingService.ConfigureEntitiesMapping(modelBuilder);
		}
	}
}