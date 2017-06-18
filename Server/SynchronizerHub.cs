using System;
using System.Collections.Generic;
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
			ITabActionDeserializer tabActionDeserializer)
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

			// TODO Synchronize is not fully transaction as browser does not take part in transaction.
			// Needs to split synchronize into smaller transactions so that AcknowledgeTabAdded does not arrive
			// before request adding finishes, use lock shared only with synchronize or store the commands to
			// sent to browser in a collection first and issue them only after transaction is committed.
			await @lock.WaitAsync();
			try
			{
				var requestData = await mPendingRequestService.GetRequestDataByPendingRequestId<AddTabRequestData>(requestId);

				// TODO Do we need to take into account the case in which the server tab's url and url passed
				// to addTab mismatched?
				mBrowserTabRepository.Add(new BrowserTab()
				{
					BrowserId = requestData.BrowserId,
					BrowserTabId = tabId,
					ServerTabId = requestData.ServerTabId
				});

				mPendingRequestService.SetRequestFulfilled(requestId);

				await mUoW.SaveChangesAsync();
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
							// TODO When to update client side tab list? Now or after receiving ack?
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
								// TODO When to update client side tab list? Now or after receiving ack?
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
								// TODO When to update client side tab list? Now or after receiving ack?
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
								// TODO When to update client side tab list? Now or after receiving ack?
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
								await Clients.Caller.ChangeTabUrl(oldTabValue.Id, newTabValue.Url);
							}

							var clientSideTab = new BrowserTab()
							{
								BrowserId = browserId,
								BrowserTabId = oldTabValue.Id,
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
					}
					else
					{
						var browserChanges = changesSinceLastConnection.Select(mTabActionDeserializer.Deserialize).ToList();

						// TODO Optimize the changes

						var browserStateOnLastUpdate = (await mBrowserTabRepository.GetAllBrowsersTabs(browserId)).ToList();

						UpdateBrowserTabIdMapping(browserStateOnLastUpdate, currentlyOpenTabs, browserChanges);

						await mUoW.SaveChangesAsync();

						// TODO Apply changes from server

						// TODO Extract into TabActionToHubMethodsDispatcher if it will work nicely with conflicts
						var newIdsByIndex = currentlyOpenTabs.ToDictionary(x => x.Index, x => x.Id); // TODO Its done second time
						foreach (var change in browserChanges)
						{
							var changesAfterThisOne = browserChanges.SkipWhile(x => x != change).Skip(1);
							var newIndex = GetTabIndexAfterChanges(change.TabIndex, changesAfterThisOne);
							int tabId;
							if (newIndex == null)
							{
								var previousIndex = GetTabIndexBeforeChanges(change.TabIndex, browserChanges.TakeWhile(x => x != change));
								if (previousIndex == null)
								{
									// Could be added and removed before update -> TODO should be optimized out
									continue;
								}

								tabId = browserStateOnLastUpdate.Single(x => x.ServerTab.Index == previousIndex).BrowserTabId;
							}
							else
							{
								tabId = newIdsByIndex[newIndex];
							}

							switch (change)
							{
								case TabCreatedDto dto:
									var serverTab = await mTabService.AddTab(browserId, tabId, dto.TabIndex, dto.Url, dto.CreateInBackground);

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
									var changedServerTab = await mTabService.ChangeTabUrl(browserId, tabId, dto.NewUrl);
									if (changedServerTab != null)
									{
										await ForEveryOtherConnectedBrowserWithTab(browserId, changedServerTab.Id,
											async (browserTabId, connectionInfo) =>
											{
												// TODO When to update client side tab list? Now or after receiving ack?
												await Clients.Client(connectionInfo.ConnectionId).ChangeTabUrl(browserTabId, dto.NewUrl);
											});
									}
									break;

								case TabMovedDto dto:
									var movedServerTab = await mTabService.MoveTab(browserId, tabId, dto.NewIndex);

									await ForEveryOtherConnectedBrowserWithTab(browserId, movedServerTab.Id,
										async (browserTabId, connectionInfo) =>
										{
											// TODO When to update client side tab list? Now or after receiving ack?
											await Clients.Client(connectionInfo.ConnectionId).MoveTab(browserTabId, dto.NewIndex);
										});
									break;

								case TabClosedDto dto:
									var closedServerTab = await mTabService.CloseTab(browserId, tabId);
									if (closedServerTab != null)
									{
										await ForEveryOtherConnectedBrowserWithTab(browserId, closedServerTab.Id,
											async (browserTabId, connectionInfo) =>
											{
												// TODO When to update client side tab list? Now or after receiving ack?
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
			IReadOnlyCollection<BrowserTab> browserTabsOnServer,
			IReadOnlyCollection<TabData> browserTabsOnBrowser,
			IReadOnlyCollection<TabAction> actionDoneSinceLastServerUpdate)
		{
			var newTabsByIndex = browserTabsOnBrowser.ToDictionary(x => x.Index);

			foreach (var tabOnServer in browserTabsOnServer)
			{
				// TODO We need to also maintain tab index for client as tab index could be changed
				// server side.
				var oldIndex = tabOnServer.ServerTab.Index.Value;
				var newIndex = GetTabIndexAfterChanges(oldIndex, actionDoneSinceLastServerUpdate);
				if (newIndex == null)
				{
					// Will be removed in the next step but id needs to be unique.
					tabOnServer.BrowserTabId = -tabOnServer.BrowserTabId;
				}
				else
				{
					tabOnServer.BrowserTabId = newTabsByIndex[newIndex].Id;
				}
			}
		}

		private int? GetTabIndexAfterChanges(int currentIndex, IEnumerable<TabAction> changes)
		{
			var newIndex = currentIndex;

			foreach (var change in changes)
			{
				switch (change)
				{
					case TabCreatedDto dto:
						if (dto.TabIndex <= newIndex)
						{
							newIndex++;
						}
						break;

					case TabClosedDto dto:
						if (dto.TabIndex == newIndex)
						{
							return null;
						}

						if (dto.TabIndex < newIndex)
						{
							newIndex--;
						}
						break;

					case TabMovedDto dto:
						if (dto.TabIndex == newIndex)
						{
							newIndex = dto.NewIndex;
						}
						else if (dto.TabIndex > newIndex && dto.NewIndex <= newIndex)
						{
							newIndex++;
						}
						else if (dto.TabIndex < newIndex && dto.NewIndex > newIndex)
						{
							newIndex--;
						}
						break;
				}
			}

			return newIndex;
		}

		// TODO Refactor -> very similar to GetTabIndexAfterChanges
		private int? GetTabIndexBeforeChanges(int newIndex, IEnumerable<TabAction> changes)
		{
			var oldIndex = newIndex;

			foreach (var change in changes.Reverse())
			{
				switch (change)
				{
					case TabCreatedDto dto:
						if (dto.TabIndex == oldIndex)
						{
							return null;
						}

						if (dto.TabIndex < oldIndex)
						{
							oldIndex--;
						}
						break;

					case TabClosedDto dto:
						if (dto.TabIndex <= oldIndex)
						{
							oldIndex++;
						}
						break;

					case TabMovedDto dto:
						if (dto.NewIndex == oldIndex)
						{
							oldIndex = dto.TabIndex;
						}
						else if (dto.TabIndex > oldIndex && dto.NewIndex <= oldIndex)
						{
							oldIndex--;
						}
						else if (dto.TabIndex < oldIndex && dto.NewIndex > oldIndex)
						{
							oldIndex++;
						}
						break;
				}
			}

			return newIndex;
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