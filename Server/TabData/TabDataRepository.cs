using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.TabData;

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
		return await mContext.Tabs.OrderBy(x => x.Index).ToListAsync();
	}

	public Task<TabData> GetByIndex(int index)
	{
		return mContext.Tabs.SingleOrDefaultAsync(x => x.Index == index);
	}

	public Task<int> GetTabCount()
	{
		return mContext.Tabs.CountAsync();
	}

	public Task IncrementTabIndices(TabRange range, int incrementBy)
	{
		var sql = @"
			UPDATE OpenTabs
			SET ""Index"" = ""Index"" + {0}
			WHERE ""Index"" >= {1} AND ""Index"" <= {2}";

		sql = Regex.Replace(sql, @"\s+", " ");

		return mContext.Database.ExecuteSqlCommandAsync(
			sql,
			CancellationToken.None,
			incrementBy,
			range.FromIndexInclusive,
			range.ToIndexInclusive);
	}
}