using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RealTimeTabSynchronizer.Server.EntityFramework;

namespace RealTimeTabSynchronizer.Server.TabData_.ClientToServerIdMapping
{
	public class BrowserTabIdServerTabIdMapper : IBrowserTabIdServerTabIdMapper
	{
		private readonly TabSynchronizerDbContext mDbContext;

		public BrowserTabIdServerTabIdMapper(TabSynchronizerDbContext dbContext)
		{
			mDbContext = dbContext;
		}

		public Task<int?> GetBrowserTabIdForServerTabId(int serverTabId)
		{
			return mDbContext.BrowserTabs
				.Where(x => x.ServerTabId == serverTabId)
				.Select(x => (int?)x.BrowserTabId)
				.SingleOrDefaultAsync();
		}
	}
}