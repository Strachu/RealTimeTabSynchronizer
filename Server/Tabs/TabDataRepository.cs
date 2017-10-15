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

namespace RealTimeTabSynchronizer.Server.TabData_
{
	public class TabDataRepository : ITabDataRepository
	{
		private readonly TabSynchronizerDbContext mContext;

		public TabDataRepository(TabSynchronizerDbContext context)
		{
			mContext = context;
		}

		public void Add(TabData tab)
		{
			mContext.Add(tab);
		}

		public void Remove(TabData tab)
		{
			mContext.Remove(tab);
		}

		public async Task<IEnumerable<TabData>> GetAllTabs()
		{
			return await mContext.Tabs
				.Where(x => x.Index != null)
				.OrderBy(x => x.Index)
				.ToListAsync();
		}

		public Task<TabData> GetByIndex(int index)
		{
			return mContext.Tabs.SingleOrDefaultAsync(x => x.Index == index);
		}

		public Task<int> GetTabCount()
		{
			return mContext.Tabs.CountAsync();
		}

		public async Task IncrementTabIndices(TabRange range, int incrementBy)
		{
			var sql = String.Empty;

			// http://stackoverflow.com/a/7703239/2579010
			if (incrementBy > 0)
			{
				sql = @"
					UPDATE ""TabData""
					SET ""Index"" = -""Index"" - {0}
					WHERE ""Index"" >= {1} AND ""Index"" <= {2};
					
					UPDATE ""TabData""
					SET ""Index"" = -""Index""
					WHERE ""Index"" < 0;";
			}
			else
			{
				sql = @"
					UPDATE ""TabData""
					SET ""Index"" = -""Index"" + {0}
					WHERE ""Index"" >= {1} AND ""Index"" <= {2};
					
					UPDATE ""TabData""
					SET ""Index"" = -""Index"" + 2 * {0}
					WHERE ""Index"" < 0;";
			}

			sql = Regex.Replace(sql, @"\s+", " ");

			await mContext.SaveChangesAsync(); // To ensure everything has been flushed - already lost some hours of debugging due to a lack of savechanges before raw sql
			await mContext.Database.ExecuteSqlCommandAsync(
				sql,
				CancellationToken.None,
				incrementBy,
				range.FromIndexInclusive,
				range.ToIndexInclusive);

			var affectedTabsInCache = mContext.Tabs.Local.Where(x =>
				x.Index >= range.FromIndexInclusive &&
				x.Index <= range.ToIndexInclusive);
			foreach (var tab in affectedTabsInCache)
			{
				var originalModificationTime = tab.LastModificationTime;

				tab.Index += incrementBy;

				// Do not issue update statements for performance reasons
				mContext.Entry(tab).Property(x => x.Index).OriginalValue = tab.Index;
				mContext.Entry(tab).Property(x => x.Index).IsModified = false;

				tab.LastModificationTime = originalModificationTime;
			}
		}
	}
}