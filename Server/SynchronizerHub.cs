using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealTimeTabSynchronizer.Server.Acknowledgments;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.DiffCalculation;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.TabData_.ClientToServerIdMapping;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server
{
	public class SynchronizerHub : Hub<IBrowserApi>
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
		private readonly IBrowserService mBrowserService;
		private readonly IPendingRequestService mPendingRequestService;
		private readonly ITabActionDeserializer mTabActionDeserializer;
		private readonly IIndexCalculator mIndexCalculator;
		private readonly IDiffCalculator mServerStateDiffCalculator;

		public SynchronizerHub(
			ILogger<SynchronizerHub> logger,
			TabSynchronizerDbContext dbContext,
			ITabService tabService,
			ITabDataRepository tabDataRepository,
			IBrowserRepository browserRepository,
			IBrowserConnectionInfoRepository connectionRepository,
			IBrowserTabIdServerTabIdMapper tabIdMapper,
			IBrowserTabRepository browserTabRepository,
			IBrowserService browserService,
			IPendingRequestService pendingRequestService,
			ITabActionDeserializer tabActionDeserializer, 
			IIndexCalculator indexCalculator, 
			IDiffCalculator serverStateDiffCalculator)
		{
			mLogger = logger;
			mUoW = dbContext;
			mTabService = tabService;
			mTabDataRepository = tabDataRepository;
			mBrowserRepository = browserRepository;
			mConnectionRepository = connectionRepository;
			mTabIdMapper = tabIdMapper;
			mBrowserTabRepository = browserTabRepository;
			mBrowserService = browserService;
			mPendingRequestService = pendingRequestService;
			mTabActionDeserializer = tabActionDeserializer;
			mIndexCalculator = indexCalculator;
			mServerStateDiffCalculator = serverStateDiffCalculator;
		}

		public async Task AddTab(Guid browserId, int tabId, int index, string url, bool createInBackground)
		{
			mLogger.LogInformation($"Adding a new tab at index {index}.");

			await @lock.WaitAsync();
			try
			{
				// TODO Incorporate transaction into UoW?
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var serverTab = await mTabService.AddTab(browserId, tabId, index, url, createInBackground);

					// TODO Think about crash reliability here... does failing to send a request for one browser
					// have to cancel adding the tab to server? The original browser will already have this added.
					var connectedBrowsers = mConnectionRepository.GetConnectedBrowsers();
					foreach (var browser in connectedBrowsers.Where(x => x.BrowserId != browserId))
					{
						await mBrowserService.AddTab(
							browser.BrowserId,
							serverTab.Id,
							serverTab.Index.Value,
							serverTab.Url,
							createInBackground);
					}

					await mUoW.SaveChangesAsync();
					transaction.Commit();
				}
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

			// TODO Synchronize is not fully transactional as browser does not take part in transaction.
			// Needs to split synchronize into smaller transactions so that AcknowledgeTabAdded does not arrive
			// before request adding finishes, use lock shared only with synchronize or store the commands to
			// sent to browser in a collection first and issue them only after transaction is committed.
			await @lock.WaitAsync();
			try
			{

				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var requestData = await mPendingRequestService.GetRequestDataByPendingRequestId<AddTabRequestData>(requestId);

					await mBrowserTabRepository.IncrementTabIndices(
						requestData.BrowserId,
						new TabRange(fromIndexInclusive: index),
						incrementBy: 1);

					// TODO Do we need to take into account the case in which the server tab's url and url passed
					// to addTab mismatched?
					mBrowserTabRepository.Add(new BrowserTab()
					{
						BrowserId = requestData.BrowserId,
						BrowserTabId = tabId,
						Index = index,
						Url = requestData.Url,
						ServerTabId = requestData.ServerTabId
					});

					mPendingRequestService.SetRequestFulfilled(requestId);

					await mUoW.SaveChangesAsync();
					transaction.Commit();
				}
			}
			finally
			{
				@lock.Release();
			}

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
						async (browserTabId, connectionInfo) =>
						{
							await Clients.Client(connectionInfo.ConnectionId).MoveTab(browserTabId, newIndex);
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
							async (browserTabId, connectionInfo) =>
							{
								await Clients.Client(connectionInfo.ConnectionId).CloseTab(browserTabId);
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
							async (browserTabId, connectionInfo) =>
							{
								await Clients.Client(connectionInfo.ConnectionId).ChangeTabUrl(browserTabId, newUrl);
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
							async (browserTabId, connectionInfo) =>
							{
								await Clients.Client(connectionInfo.ConnectionId).ActivateTab(browserTabId);
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
			IReadOnlyCollection<object> changesSinceLastConnection,
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

							var connectedBrowsers = mConnectionRepository.GetConnectedBrowsers();
							foreach (var otherBrowser in connectedBrowsers.Where(x => x.BrowserId != browserId))
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
								await Clients.Caller.ChangeTabUrl(oldTabValue.Id, newTabValue.Url);
							}

							var clientSideTab = new BrowserTab()
							{
								BrowserId = browserId,
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
								browserId,
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

							await Clients.Caller.CloseTab(openTab.Id);
						}
					}
					else
					{
						var browserChanges = changesSinceLastConnection.Select(mTabActionDeserializer.Deserialize).ToList();

						// TODO Optimize the changes

						var browserStateOnLastUpdate = (await mBrowserTabRepository.GetAllBrowsersTabs(browserId)).ToList();

						UpdateBrowserTabIdMapping(browserStateOnLastUpdate, currentlyOpenTabs, browserChanges);

						await mUoW.SaveChangesAsync();

						await ApplyChangesFromServerToBrowser(browserId, browserStateOnLastUpdate);

						// TODO Extract into TabActionToHubMethodsDispatcher if it will work nicely with conflicts
						var oldIdsByIndex = browserStateOnLastUpdate.ToDictionary(x => x.Index, x => x.BrowserTabId);
						var newIdsByIndex = currentlyOpenTabs.ToDictionary(x => x.Index.Value, x => x.Id);
						foreach (var change in browserChanges)
						{
							var tabId = GetTabIdOfChangedTab(change, browserChanges, oldIdsByIndex, newIdsByIndex);
							if (tabId == null)
							{
								continue;
							}

							switch (change)
							{
								case TabCreatedDto dto:
									var serverTab = await mTabService.AddTab(browserId, tabId.Value, dto.TabIndex, dto.Url, dto.CreateInBackground);

									// Again... EF is annoying with it's ids... switch over to guids?
									await mUoW.SaveChangesAsync(); // Retrieve automatically assigned ids from database

									var connectedBrowsers = mConnectionRepository.GetConnectedBrowsers();
									foreach (var otherBrowser in connectedBrowsers.Where(x => x.BrowserId != browserId))
									{
										await mBrowserService.AddTab(
											otherBrowser.BrowserId,
											serverTab.Id,
											serverTab.Index.Value,
											serverTab.Url,
											dto.CreateInBackground);
									}
									break;

								case TabUrlChangedDto dto:
									var changedServerTab = await mTabService.ChangeTabUrl(browserId, tabId.Value, dto.NewUrl);
									if (changedServerTab != null)
									{
										await ForEveryOtherConnectedBrowserWithTab(browserId, changedServerTab.Id,
											async (browserTabId, connectionInfo) =>
											{
												await Clients.Client(connectionInfo.ConnectionId).ChangeTabUrl(browserTabId, dto.NewUrl);
											});
									}
									break;

								case TabMovedDto dto:
									var movedServerTab = await mTabService.MoveTab(browserId, tabId.Value, dto.NewIndex);

									await ForEveryOtherConnectedBrowserWithTab(browserId, movedServerTab.Id,
										async (browserTabId, connectionInfo) =>
										{
											await Clients.Client(connectionInfo.ConnectionId).MoveTab(browserTabId, dto.NewIndex);
										});
									break;

								case TabClosedDto dto:
									var closedServerTab = await mTabService.CloseTab(browserId, tabId.Value);
									if (closedServerTab != null)
									{
										await ForEveryOtherConnectedBrowserWithTab(browserId, closedServerTab.Id,
											async (browserTabId, connectionInfo) =>
											{
												await Clients.Client(connectionInfo.ConnectionId).CloseTab(browserTabId);
											});
									}
									break;
							}
						}
					}

					// TODO What with active tab??

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

		private void UpdateBrowserTabIdMapping(
			IEnumerable<BrowserTab> browserTabsOnServer,
			IEnumerable<TabData> browserTabsOnBrowser,
			IReadOnlyCollection<TabAction> actionsDoneSinceLastServerUpdate)
		{
			var upToDateBrowserTabsByIndex = browserTabsOnBrowser.ToDictionary(x => x.Index);

			foreach (var tabOnServer in browserTabsOnServer)
			{
				var newIndex = mIndexCalculator.GetTabIndexAfterChanges(tabOnServer.Index, actionsDoneSinceLastServerUpdate);
				if (newIndex == null)
				{
					// Will be removed in the next step but id needs to be unique.
					tabOnServer.BrowserTabId = -tabOnServer.BrowserTabId;
				}
				else
				{
					tabOnServer.BrowserTabId = upToDateBrowserTabsByIndex[newIndex].Id;
				}
			}
		}

		private async Task ApplyChangesFromServerToBrowser(Guid browserId, IReadOnlyCollection<BrowserTab> browserStateOnLastUpdate)
		{
			var serverState = (await mTabDataRepository.GetAllTabs()).ToList();
			var serverSideChanges = mServerStateDiffCalculator.ComputeChanges(browserStateOnLastUpdate, serverState).ToList();

			foreach (var change in serverSideChanges)
			{
				if (change is TabCreatedDto addTabAction)
				{
					var changesAfterThisOne = serverSideChanges.SkipWhile(x => x != addTabAction).Skip(1);
					var finalServerTabIndex = mIndexCalculator.GetTabIndexAfterChanges(addTabAction.TabIndex, changesAfterThisOne);
					var serverTabId = serverState.Single(x => x.Index == finalServerTabIndex).Id;

					await mBrowserService.AddTab(
						browserId,
						serverTabId,
						addTabAction.TabIndex,
						addTabAction.Url,
						addTabAction.CreateInBackground);

					continue;
				}

				var idsByOldIndex = browserStateOnLastUpdate.ToDictionary(x => x.Index, x => x.BrowserTabId);
				var idsByNewIndex = serverState
					.Where(x => x.IsOpen)
					.Select(x => new
					{
						Index = x.Index.Value,
						BrowserTab = browserStateOnLastUpdate.SingleOrDefault(y => y.ServerTabId == x.Id)
					})
					.Where(x => x.BrowserTab != null)
					.ToDictionary(x => x.Index, x => x.BrowserTab.BrowserTabId);
				var browserTabId = GetTabIdOfChangedTab(change, serverSideChanges, idsByOldIndex, idsByNewIndex);
				if (browserTabId == null)
				{
					throw new InvalidOperationException("BrowserTabId should not be null for actions other than add tab");
				}

				switch (change)
				{
					case TabUrlChangedDto dto:
						await Clients.Caller.ChangeTabUrl(browserTabId.Value, dto.NewUrl);
						break;

					case TabMovedDto dto:
						await Clients.Caller.MoveTab(browserTabId.Value, dto.NewIndex);
						break;

					case TabClosedDto dto:
						await Clients.Caller.CloseTab(browserTabId.Value);
						break;
				}
			}
		}

		private int? GetTabIdOfChangedTab(
			TabAction change,
			IReadOnlyCollection<TabAction> allChanges,
			IDictionary<int, int> idsByIndexBeforeChanges,
			IDictionary<int, int> idsByIndexAfterChanges)
		{
			// TODO Unit test for TabClosedDto - crashed before
			if (!(change is TabClosedDto))
			{
				var tabMovedChange = change as TabMovedDto;
				var currentIndex = (tabMovedChange == null) ? change.TabIndex : tabMovedChange.NewIndex;

				var changesAfterThisOne = allChanges.SkipWhile(x => x != change).Skip(1);
				var newIndex = mIndexCalculator.GetTabIndexAfterChanges(currentIndex, changesAfterThisOne);
				if (newIndex != null)
				{
					return idsByIndexAfterChanges[newIndex.Value];
				}
			}

			// TODO Unit test for it - crashed before
			if (change is TabCreatedDto)
			{
				return null;
			}

			var previousIndex = mIndexCalculator.GetTabIndexBeforeChanges(change.TabIndex, allChanges.TakeWhile(x => x != change));
			if (previousIndex == null)
			{
				// Could be added and removed before update -> TODO should be optimized out
				return null;
			}

			return idsByIndexBeforeChanges[previousIndex.Value];
		}

		public override Task OnConnected()
		{
			SaveConnection();

			return base.OnConnected();
		}

		public override Task OnReconnected()
		{
			SaveConnection();

			return base.OnReconnected();
		}

		public override Task OnDisconnected(bool stopCalled)
		{
			mConnectionRepository.RemoveConnection(Context.ConnectionId);

			return base.OnDisconnected(stopCalled);
		}

		private void SaveConnection()
		{
			var connectionId = Context.ConnectionId;
			var browserId = Guid.Parse(Context.QueryString["browserId"]);

			mConnectionRepository.AddConnection(browserId, connectionId);
		}

		private async Task ForEveryOtherConnectedBrowserWithTab(
			Guid currentBrowserId,
			int serverTabId,
			 Func<int, BrowserConnectionInfo, Task> action)
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

				await action(browserTabId.Value, browser);
			}
		}
	}
}