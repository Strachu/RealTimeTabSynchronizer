using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server.Tabs.Browsers
{
	// TODO There's a lot of duplication with ServerTabRepository -> merge it?
	public class BrowserTabRepository : IBrowserTabRepository
	{
		private readonly TabSynchronizerDbContext mContext;

		public BrowserTabRepository(TabSynchronizerDbContext context)
		{
			mContext = context;
		}

		public void Add(BrowserTab tab)
		{
			mContext.Add(tab);
		}

		public async Task<IEnumerable<BrowserTab>> GetAllBrowsersTabs(Guid browserId)
		{
			return await mContext.BrowserTabs.OrderBy(x => x.ServerTab.Index).ToListAsync();
		}

		public Task<BrowserTab> GetByBrowserTabId(Guid browserId, int tabId)
		{
			return mContext.BrowserTabs
				.Include(x => x.ServerTab)
				.SingleOrDefaultAsync(x => x.BrowserId == browserId && x.BrowserTabId == tabId);
		}

		public void Remove(BrowserTab tab)
		{
			mContext.Remove(tab);
		}

		// public Task IncrementTabIndices(Guid browserId, TabRange range, int incrementBy)
		// {
		// 	var sql = @"
		// 		UPDATE ""BrowserTabs""
		// 		SET ""Index"" = ""Index"" + {0}
		// 		WHERE ""Index"" >= {1} AND ""Index"" <= {2}";

		// 	sql = Regex.Replace(sql, @"\s+", " ");

		// 	return mContext.Database.ExecuteSqlCommandAsync(
		// 		sql,
		// 		CancellationToken.None,
		// 		incrementBy,
		// 		range.FromIndexInclusive,
		// 		range.ToIndexInclusive);
		// }
	}
}