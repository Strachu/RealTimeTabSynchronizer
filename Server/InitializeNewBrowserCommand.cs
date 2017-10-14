using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RealTimeTabSynchronizer.Server.Acknowledgments;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.DiffCalculation;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.TabData_.ClientToServerIdMapping;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server
{
	public class InitializeNewBrowserCommand : IInitializeNewBrowserCommand
	{
		private readonly ILogger mLogger;
		private readonly TabSynchronizerDbContext mUoW;
		private readonly ITabService mTabService;
		private readonly ITabDataRepository mTabDataRepository;
		private readonly IBrowserRepository mBrowserRepository;
		private readonly IBrowserConnectionInfoRepository mConnectionRepository;
		private readonly IBrowserTabRepository mBrowserTabRepository;
		private readonly IBrowserService mBrowserService;

		public InitializeNewBrowserCommand(
			ILogger<InitializeNewBrowserCommand> logger,
			TabSynchronizerDbContext dbContext,
			ITabService tabService,
			ITabDataRepository tabDataRepository,
			IBrowserRepository browserRepository,
			IBrowserConnectionInfoRepository connectionRepository,
			IBrowserTabRepository browserTabRepository,
			IBrowserService browserService)
		{
			mLogger = logger;
			mUoW = dbContext;
			mTabService = tabService;
			mTabDataRepository = tabDataRepository;
			mBrowserRepository = browserRepository;
			mConnectionRepository = connectionRepository;
			mBrowserTabRepository = browserTabRepository;
			mBrowserService = browserService;
		}

		public async Task ExecuteAsync(IBrowserApi browser, Browser browserInfo, IReadOnlyCollection<TabData> currentlyOpenTabs)
		{
			mLogger.LogDebug(
				$"InitializeNewBrowser(browserInfo: ({browserInfo}), currentlyOpenTabs: " + Environment.NewLine +
				$"{String.Join(";\n", currentlyOpenTabs)})");

			mBrowserRepository.Add(new Browser
			{
				Id = browserInfo.Id,
				Name = browserInfo.Name
			});

			var tabsAlreadyOnServer = (await mTabDataRepository.GetAllTabs()).ToList();
			var tabsAlreadyOnServerByUrl = tabsAlreadyOnServer.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
			var newTabs = currentlyOpenTabs.Where(x => !tabsAlreadyOnServerByUrl.ContainsKey(x.Url)).ToList();

			mLogger.LogDebug($"Tabs already on server: {tabsAlreadyOnServer.Count}");
			mLogger.LogDebug($"Tabs already on server ignoring duplicates: {tabsAlreadyOnServerByUrl.Count}");
			mLogger.LogDebug($"Browser tabs: {currentlyOpenTabs.Count}");
			mLogger.LogDebug($"New tabs: {newTabs.Count}");

			var allServerTabs = new List<TabData>(tabsAlreadyOnServer);
			for (int i = 0; i < newTabs.Count; ++i)
			{
				var newTab = await mTabService.AddTab(tabsAlreadyOnServer.Count + i, newTabs[i].Url, createInBackground: true);

				await mUoW.SaveChangesAsync(); // Retrieve automatically assigned id from database

				var connectedBrowsers = await mConnectionRepository.GetConnectedBrowsers();
				foreach (var otherBrowser in connectedBrowsers.Where(x => x.BrowserId != browserInfo.Id))
				{
					await mBrowserService.AddTab(
						otherBrowser.BrowserId,
						newTab.Id,
						newTab.Index.Value,
						newTab.Url,
						createInBackground: true);
				}

				allServerTabs.Add(newTab);
			}

			var tabsSortedByIndex = currentlyOpenTabs.OrderBy(x => x.Index).ToList();
			var tabsToUpdate = Math.Min(allServerTabs.Count, tabsSortedByIndex.Count);
			for (int i = 0; i < tabsToUpdate; ++i)
			{
				var oldTabValue = tabsSortedByIndex[i];
				var newTabValue = allServerTabs[i];

				if (!oldTabValue.Url.Equals(newTabValue.Url, StringComparison.OrdinalIgnoreCase))
				{
					// TODO This does not throw when client has disconnected!
					await browser.ChangeTabUrl(oldTabValue.Id, newTabValue.Url);
				}

				var clientSideTab = new BrowserTab()
				{
					BrowserId = browserInfo.Id,
					BrowserTabId = oldTabValue.Id,
					Index = i,
					Url = newTabValue.Url,
					ServerTab = allServerTabs[i]
				};
				mBrowserTabRepository.Add(clientSideTab);
			}

			await mUoW.SaveChangesAsync(); // Retrieve automatically assigned ids from database

			for (int i = currentlyOpenTabs.Count; i < allServerTabs.Count; ++i)
			{
				var serverTab = allServerTabs[i];
				await mBrowserService.AddTab(
						browserInfo.Id,
						serverTab.Id,
						serverTab.Index.Value,
						serverTab.Url,
						createInBackground: true);
			}

			// Test Case:
			// New browser is added with duplicate tabs which already exists on server
			// The duplicated tab is not added to server but remaining open tabs are not closed
			// TODO Convert test case to unit test
			for (int i = currentlyOpenTabs.Count - 1; i >= allServerTabs.Count; --i)
			{
				var openTab = tabsSortedByIndex[i];

				await browser.CloseTab(openTab.Id);
			}
		}
	}
}