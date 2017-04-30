using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server
{
	public class TabService : ITabService
	{
		private readonly ILogger mLogger;
		private readonly ITabDataRepository mTabDataRepository;
		private readonly IActiveTabDao mActiveTabDao;

		public TabService(
			ILogger<TabService> logger,
			ITabDataRepository tabDataRepository,
			IActiveTabDao activeTabDao)
		{
			mLogger = logger;
			mTabDataRepository = tabDataRepository;
			mActiveTabDao = activeTabDao;
		}

		public async Task<string> AddTab(int tabIndex, string url, bool createInBackground)
		{
			if(url.Equals("about:newtab", StringComparison.OrdinalIgnoreCase))
			{
				// TODO this should be done in firefox addon as it is specific to a browser.
				url = "about:blank"; // Firefox for android ignores tabs with "about:newtab".
			}

			await mTabDataRepository.IncrementTabIndices(
				new TabRange(fromIndexInclusive: tabIndex),
				incrementBy: 1);	

			mTabDataRepository.Add(new TabData { Index = tabIndex, Url = url });
			return url;
		}

		public async Task MoveTab(int oldTabIndex, int newTabIndex)
		{
			var tab = await mTabDataRepository.GetByIndex(oldTabIndex);

			if(oldTabIndex < newTabIndex)
			{
				await mTabDataRepository.IncrementTabIndices(
					new TabRange(oldTabIndex + 1, newTabIndex),
					incrementBy: -1);
			}
			else
			{
				await mTabDataRepository.IncrementTabIndices(
					new TabRange(newTabIndex, newTabIndex - 1),
					incrementBy: 1);
			}
		
			tab.Index = newTabIndex;
		}

		public async Task CloseTab(int tabIndex)
		{
			var tab = await mTabDataRepository.GetByIndex(tabIndex);

			// EF doesn't play nice with raw sql due to it's UoW.
			await mTabDataRepository.Remove(tab, forceFlush: true);

			await mTabDataRepository.IncrementTabIndices(
				new TabRange(fromIndexInclusive: tabIndex + 1),
				incrementBy: -1);
		}

		public async Task<bool> ChangeTabUrl(int tabIndex, string newUrl)
		{
			var tab = await mTabDataRepository.GetByIndex(tabIndex);
			if(tab.Url.Equals(newUrl, StringComparison.OrdinalIgnoreCase))
			{	
				mLogger.LogDebug($"The url did not change.");
				return false;
			}

			tab.Url = newUrl;
			return true;
		}

		public async Task ActivateTab(int tabIndex)
		{
			var tab = await mTabDataRepository.GetByIndex(tabIndex);
			await mActiveTabDao.SetActiveTab(tab);		
		}
	}
}