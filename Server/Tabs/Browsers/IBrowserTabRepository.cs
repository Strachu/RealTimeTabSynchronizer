using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server.Tabs.Browsers
{
	public interface IBrowserTabRepository
	{
		void Add(BrowserTab tab);
		Task<IEnumerable<BrowserTab>> GetAllBrowsersTabs(Guid browserId);
		Task<BrowserTab> GetByBrowserTabId(Guid browserId, int tabId);
		void Remove(BrowserTab tab);

		Task IncrementTabIndices(Guid browserId, TabRange range, int incrementBy);
	}
}