using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
	public class ActiveTabDao : IActiveTabDao
	{
		private readonly TabSynchronizerDbContext mContext;

		public ActiveTabDao(TabSynchronizerDbContext context)
		{
			mContext = context;
		}

		public async Task<TabData> GetActiveTab()
		{
			var activeTab = await mContext.ActiveTab.SingleOrDefaultAsync();

			return activeTab?.Tab;			
		}

		public async Task SetActiveTab(TabData tab)
		{
			var activeTab = await mContext.ActiveTab.SingleOrDefaultAsync();
			if(activeTab == null)
			{
				activeTab = new ActiveTab();
				mContext.ActiveTab.Add(activeTab);
			}

			activeTab.Tab = tab;
		}
	}
}
