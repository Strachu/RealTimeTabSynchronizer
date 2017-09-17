using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server
{
	public class TabService : ITabService
	{
		private readonly TabSynchronizerDbContext mDbContext;
		private readonly ILogger mLogger;
		private readonly ITabDataRepository mTabDataRepository;
		private readonly IBrowserTabRepository mBrowserTabRepository;
		private readonly IActiveTabDao mActiveTabDao;

		public TabService(
			TabSynchronizerDbContext dbContext,
			ILogger<TabService> logger,
			ITabDataRepository tabDataRepository,
			IBrowserTabRepository browserTabRepository,
			IActiveTabDao activeTabDao)
		{
			mDbContext = dbContext;
			mLogger = logger;
			mTabDataRepository = tabDataRepository;
			mBrowserTabRepository = browserTabRepository;
			mActiveTabDao = activeTabDao;
		}

		public async Task<TabData> AddTab(int tabIndex, string url, bool createInBackground)
		{
			if (url.Equals("about:newtab", StringComparison.OrdinalIgnoreCase))
			{
				// TODO this should be done in firefox addon as it is specific to a browser.
				url = "about:blank"; // Firefox for android ignores tabs with "about:newtab".
			}

			await mTabDataRepository.IncrementTabIndices(
				new TabRange(fromIndexInclusive: tabIndex),
				incrementBy: 1);

			var newTab = new TabData { Index = tabIndex, Url = url };

			mTabDataRepository.Add(newTab);

			return newTab;
		}

		public async Task<TabData> AddTab(Guid browserId, int tabId, int tabIndex, string url, bool createInBackground)
		{
			var newTab = await AddTab(tabIndex, url, createInBackground);
			
			await mBrowserTabRepository.IncrementTabIndices(
				browserId,
				new TabRange(fromIndexInclusive: tabIndex),
				incrementBy: 1);
			
			var browserTab = new BrowserTab
			{
				BrowserId = browserId,
				BrowserTabId = tabId,
				Index = tabIndex,
				ServerTab = newTab
			};

			mBrowserTabRepository.Add(browserTab);

			return newTab;
		}

		public async Task<TabData> MoveTab(Guid browserId, int tabId, int newTabIndex)
		{
			var tab = await mBrowserTabRepository.GetByBrowserTabId(browserId, tabId);
			if (tab == null)
			{
				// TODO Logging of every exception...
				throw new ArgumentException($"Tab {tabId} on browser {browserId} does not exist!");
			}

			if (tab.ServerTab.Index != newTabIndex && tab.ServerTab.Index != null)
			{
				// To prevent UNIQUE violation when decrementing other tabs indices.
				var oldTabIndex = tab.ServerTab.Index.Value;
				tab.ServerTab.Index = null;

				// EF doesn't play nice with raw sql due to it's UoW.
				await mDbContext.SaveChangesAsync();

				if (oldTabIndex < newTabIndex)
				{
					await mTabDataRepository.IncrementTabIndices(
						new TabRange(oldTabIndex + 1, newTabIndex),
						incrementBy: -1);
				}
				else
				{
					await mTabDataRepository.IncrementTabIndices(
						new TabRange(newTabIndex, oldTabIndex - 1),
						incrementBy: 1);
				}

				tab.ServerTab.Index = newTabIndex;
			}

			if (tab.Index != newTabIndex)
			{
				var oldTabIndex = tab.Index;
				tab.Index = Int32.MaxValue;

				await mDbContext.SaveChangesAsync();

				if (oldTabIndex < newTabIndex)
				{
					await mBrowserTabRepository.IncrementTabIndices(
						browserId,
						new TabRange(oldTabIndex + 1, newTabIndex),
						incrementBy: -1);
				}
				else
				{
					await mBrowserTabRepository.IncrementTabIndices(
						browserId,
						new TabRange(newTabIndex, oldTabIndex - 1),
						incrementBy: 1);
				}
				
				tab.Index = newTabIndex;
			}

			return tab.ServerTab;
		}

		public async Task<TabData> CloseTab(Guid browserId, int tabId)
		{
			var tab = await mBrowserTabRepository.GetByBrowserTabId(browserId, tabId);
			if (tab == null)
			{
				mLogger.LogDebug($"Tab {tabId} on {browserId} has already been closed or never existed.");
				return null;
			}

			if (tab.ServerTab.IsOpen)
			{
				var tabIndex = tab.ServerTab.Index.Value;

				tab.ServerTab.IsOpen = false;

				// EF doesn't play nice with raw sql due to it's UoW.
				await mDbContext.SaveChangesAsync();

				await mTabDataRepository.IncrementTabIndices(
					new TabRange(fromIndexInclusive: tabIndex + 1),
					incrementBy: -1);
			}

			// TODO Cleaning of not referenced server tabs?
			mBrowserTabRepository.Remove(tab);

			await mDbContext.SaveChangesAsync();
			await mBrowserTabRepository.IncrementTabIndices(
				browserId,
				new TabRange(fromIndexInclusive: tab.Index + 1),
				incrementBy: -1);
			
			return tab.ServerTab;
		}

		public async Task<TabData> ChangeTabUrl(Guid browserId, int tabId, string newUrl)
		{
			var tab = await mBrowserTabRepository.GetByBrowserTabId(browserId, tabId);
			if (tab == null)
			{
				throw new ArgumentException($"Tab {tabId} on browser {browserId} does not exist!");
			}

			if (tab.ServerTab.Url.Equals(newUrl, StringComparison.OrdinalIgnoreCase))
			{
				mLogger.LogDebug($"The url did not change.");
				return null;
			}

			tab.ServerTab.Url = newUrl;
			return tab.ServerTab;
		}

		public async Task<TabData> ActivateTab(Guid browserId, int tabId)
		{
			var tab = await mBrowserTabRepository.GetByBrowserTabId(browserId, tabId);
			if (tab == null)
			{
				throw new ArgumentException($"Tab {tabId} on browser {browserId} does not exist!");
			}

			await mActiveTabDao.SetActiveTab(tab.ServerTab);
			return tab.ServerTab;
		}
	}
}