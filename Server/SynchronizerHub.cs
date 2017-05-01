using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.TabData_.ClientToServerIdMapping;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server
{
	public class SynchronizerHub : Hub
	{
		private static SemaphoreSlim @lock = new SemaphoreSlim(initialCount: 1);

		private readonly ILogger mLogger;
		private readonly TabSynchronizerDbContext mUoW;
		private readonly ITabService mTabService;
		private readonly ITabDataRepository mTabDataRepository;
		private readonly IBrowserRepository mBrowserRepository;
		private readonly IBrowserConnectionInfoRepository mConnectionRepository;
		private readonly IBrowserTabIdServerTabIdMapper mTabIdMapper;
		private readonly IBrowserTabRepository mBrowserTabRepository;

		public SynchronizerHub(
			ILogger<SynchronizerHub> logger,
			TabSynchronizerDbContext dbContext,
			ITabService tabService,
			ITabDataRepository tabDataRepository,
			IBrowserRepository browserRepository,
			IBrowserConnectionInfoRepository connectionRepository,
			IBrowserTabIdServerTabIdMapper tabIdMapper,
			IBrowserTabRepository browserTabRepository)
		{
			mLogger = logger;
			mUoW = dbContext;
			mTabService = tabService;
			mTabDataRepository = tabDataRepository;
			mBrowserRepository = browserRepository;
			mConnectionRepository = connectionRepository;
			mTabIdMapper = tabIdMapper;
			mBrowserTabRepository = browserTabRepository;
		}

		public async Task AddTab(Guid browserId, int tabId, int index, string url, bool createInBackground)
		{
			mLogger.LogInformation($"Adding a new tab at index {index}.");

			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var serverTab = await mTabService.AddTab(browserId, tabId, index, url, createInBackground);
					await mUoW.SaveChangesAsync();

					transaction.Commit();

					url = serverTab.Url;
				}

				await Clients.Others.addTab(Guid.NewGuid(), index, url, createInBackground);
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished adding new tab.");
		}

		public async Task AcknowledgeTabAdded(Guid requestId, int tabId, int index)
		{
			mLogger.LogInformation($"Got an acknowledge for tab adding request {requestId}.");

			// TODO Add the tab to client tabs state or experiment with TaskCompletionSource and await

			mLogger.LogInformation("Finished handling the acknowledge for adding request.");
		}

		public async Task MoveTab(Guid browserId, int tabId, int newIndex)
		{
			mLogger.LogInformation($"Moving a tab {tabId} at {newIndex}.");

			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var movedServerTab = await mTabService.MoveTab(browserId, tabId, newIndex);

					await ForEveryOtherConnectedBrowserWithTab(browserId, movedServerTab.Id,
						async (browserTabId, connectionId) =>
						{
							// TODO When to update client side tab list? Now or after receiving ack?
							await Clients.Client(connectionId).moveTab(browserTabId, newIndex);
						});

					await mUoW.SaveChangesAsync();
					transaction.Commit();
				}
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished moving a tab.");
		}

		public async Task CloseTab(Guid browserId, int tabId)
		{
			mLogger.LogInformation("Closing a tab.");

			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var closedServerTab = await mTabService.CloseTab(browserId, tabId);
					if (closedServerTab != null)
					{
						await ForEveryOtherConnectedBrowserWithTab(browserId, closedServerTab.Id,
							async (browserTabId, connectionId) =>
							{
								// TODO When to update client side tab list? Now or after receiving ack?
								await Clients.Client(connectionId).closeTab(browserTabId);
							});
					}

					await mUoW.SaveChangesAsync();
					transaction.Commit();
				}
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished closing a tab.");
		}

		public async Task ChangeTabUrl(Guid browserId, int tabId, string newUrl)
		{
			mLogger.LogInformation($"Changing tab {tabId} url to {newUrl}.");

			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var changedServerTab = await mTabService.ChangeTabUrl(browserId, tabId, newUrl);
					if (changedServerTab != null)
					{
						await ForEveryOtherConnectedBrowserWithTab(browserId, changedServerTab.Id,
							async (browserTabId, connectionId) =>
							{
								// TODO When to update client side tab list? Now or after receiving ack?
								await Clients.Client(connectionId).changeTabUrl(browserTabId, newUrl);
							});
					}

					await mUoW.SaveChangesAsync();
					transaction.Commit();
				}
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished changing a url of tab.");
		}

		public async Task ActivateTab(Guid browserId, int tabId)
		{
			mLogger.LogInformation($"Activating tab {tabId}.");

			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var activatedServerTab = await mTabService.ActivateTab(browserId, tabId);
					if (activatedServerTab != null)
					{
						await ForEveryOtherConnectedBrowserWithTab(browserId, activatedServerTab.Id,
							async (browserTabId, connectionId) =>
							{
								// TODO When to update client side tab list? Now or after receiving ack?
								await Clients.Client(connectionId).activateTab(browserTabId);
							});
					}

					await mUoW.SaveChangesAsync();
					transaction.Commit();
				}
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished activating a tab.");
		}

		public async Task Synchronize(
			Guid browserId,
			IReadOnlyCollection<JObject> changesSinceLastConnection,
			IReadOnlyCollection<TabData> currentlyOpenTabs)
		{
			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					mLogger.LogInformation($"[{browserId}]: Synchronizing {changesSinceLastConnection.Count} changes " +
						$"with currently {currentlyOpenTabs.Count} open tabs...");

					var browser = await mBrowserRepository.GetById(browserId);
					if (browser == null)
					{
						var browserName = Context.Request.Headers["User-Agent"];
						mBrowserRepository.Add(new Browser
						{
							Id = browserId,
							Name = browserName
						});

						var tabsAlreadyOnServer = (await mTabDataRepository.GetAllTabs()).ToList();
						var tabsAlreadyOnServerByUrl = tabsAlreadyOnServer.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
						var openTabsWithoutDuplicates = currentlyOpenTabs.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
						var newTabs = openTabsWithoutDuplicates.Where(x => !tabsAlreadyOnServerByUrl.ContainsKey(x.Url)).ToList();

						mLogger.LogDebug($"Tabs already on server: {tabsAlreadyOnServer.Count}");
						mLogger.LogDebug($"Tabs already on server ignoring duplicates: {tabsAlreadyOnServerByUrl.Count}");
						mLogger.LogDebug($"Browser tabs: {currentlyOpenTabs.Count}");
						mLogger.LogDebug($"Browser tags ignoring duplicates: {openTabsWithoutDuplicates.Count}");
						mLogger.LogDebug($"New tabs: {newTabs.Count}");

						var allServerTabs = new List<TabData>(tabsAlreadyOnServer);
						for (int i = 0; i < newTabs.Count; ++i)
						{
							// TODO what with ACK?
							var newTab = await mTabService.AddTab(tabsAlreadyOnServer.Count + i, newTabs[i].Url, createInBackground: true);
							allServerTabs.Add(newTab);

							await Clients.Others.addTab(Guid.NewGuid(), tabsAlreadyOnServer.Count + i, newTabs[i].Url, createInBackground: true);
						}

						var tabsSortedByIndex = currentlyOpenTabs.OrderBy(x => x.Index).ToList();
						var tabsToUpdate = Math.Min(allServerTabs.Count, tabsSortedByIndex.Count);
						for (int i = 0; i < tabsToUpdate; ++i)
						{
							var oldTabValue = tabsSortedByIndex[i];
							var newTabValue = allServerTabs[i];

							if (!oldTabValue.Url.Equals(newTabValue.Url, StringComparison.OrdinalIgnoreCase))
							{
								await Clients.Caller.changeTabUrl(oldTabValue.Id, newTabValue.Url);
							}

							var clientSideTab = new BrowserTab()
							{
								BrowserId = browserId,
								BrowserTabId = oldTabValue.Id,
								ServerTab = allServerTabs[i]
							};
							mBrowserTabRepository.Add(clientSideTab);
						}

						if (allServerTabs.Count > newTabs.Count)
						{
							for (int i = currentlyOpenTabs.Count; i < allServerTabs.Count; ++i)
							{
								// TODO what with ACK? We do not know tab Id here
								// Should probably do the same as in AddTab for others.
								await Clients.Caller.addTab(Guid.NewGuid(), i, allServerTabs[i].Url, createInBackground: true);

								// var clientSideTab = new BrowserTab()
								// {
								// 	BrowserId = browserId,
								// 	BrowserTabId = oldTabValue.Id,
								// 	ServerTab = tabsAlreadyOnServer[i]
								// };
								// mBrowserTabRepository.Add(clientSideTab);
							}
						}
						else
						{
							// TODO Should we remove duplicates on first run? Maybe leave the tabs as is?
							var duplicatesToRemove = currentlyOpenTabs.Except(openTabsWithoutDuplicates);
							foreach (var tabToRemove in duplicatesToRemove)
							{
								await Clients.Caller.closeTab(tabToRemove.Id);
							}
						}
					}
					else
					{
						// TODO Map changesSinceLastConnection to classes.

						// TODO Compute diff, solve any conflicts

						// TODO Update Mapping
					}

					await mUoW.SaveChangesAsync();
					transaction.Commit();
				}
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation($"Finished synchronizing tabs...");
		}

		public override Task OnConnected()
		{
			var connectionId = Context.ConnectionId;
			var browserId = Guid.Parse(Context.QueryString["browserId"]);

			mConnectionRepository.AddConnection(browserId, connectionId);

			return base.OnConnected();
		}

		public override Task OnDisconnected(bool stopCalled)
		{
			mConnectionRepository.RemoveConnection(Context.ConnectionId);

			return base.OnDisconnected(stopCalled);
		}

		private async Task ForEveryOtherConnectedBrowserWithTab(
			Guid currentBrowserId,
			int serverTabId,
			 Func<int, string, Task> action)
		{
			var connectedBrowsers = mConnectionRepository.GetConnectedBrowsers();
			foreach (var browser in connectedBrowsers.Where(x => x.BrowserId != currentBrowserId))
			{
				var browserTabId = await mTabIdMapper.GetBrowserTabIdForServerTabId(serverTabId);
				if (browserTabId == null)
				{
					mLogger.LogWarning($"Browser {browser.BrowserId} did not have tab {serverTabId}.");
					continue;
				}

				await action(browserTabId.Value, browser.ConnectionId);
			}
		}
	}
}