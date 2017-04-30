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

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public class BrowserRepository : IBrowserRepository
	{
		private readonly TabSynchronizerDbContext mContext;

		public BrowserRepository(TabSynchronizerDbContext context)
		{
			mContext = context;
		}

		public void Add(Browser tab)
		{
			mContext.Browsers.Add(tab);
		}

		public Task<Browser> GetById(Guid id)
		{
			return mContext.Browsers.FindAsync(id);
		}
	}
}