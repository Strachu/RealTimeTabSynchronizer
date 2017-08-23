using Microsoft.EntityFrameworkCore;

namespace RealTimeTabSynchronizer.Server.EntityFramework
{
	public class DbContextFactory
	{
		private readonly DbContextOptions<TabSynchronizerDbContext> mContextOptions;
		private readonly IModelBuildingService mModelBuildingService;

		public DbContextFactory(
			DbContextOptions<TabSynchronizerDbContext> contextOptions,
			IModelBuildingService modelBuildingService)
		{
			mContextOptions = contextOptions;
			mModelBuildingService = modelBuildingService;
		}

		public TabSynchronizerDbContext Create()
		{
			return new TabSynchronizerDbContext(mContextOptions, mModelBuildingService);
		}
	}
}