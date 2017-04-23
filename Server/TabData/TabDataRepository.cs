using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public class TabDataRepository : ITabDataRepository
{
	private readonly TabSynchronizerDbContext mContext;

	public TabDataRepository(TabSynchronizerDbContext context)
	{
		mContext = context;
	}

	public void AddTab(TabData tab)
	{
		mContext.Add(tab);
	}

	public async Task<IEnumerable<TabData>> GetAllTabs()
	{
		return await mContext.Tabs.OrderBy(x => x.Index).ToListAsync();
	}

	public Task<int> GetTabCount()
	{
		return mContext.Tabs.CountAsync();
	}
}