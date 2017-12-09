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
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using RealTimeTabSynchronizer.Server.Acknowledgments;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.ChangeHistory;
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
		private readonly IChangeListOptimizer mChangeListOptimizer;
		private readonly IChangeHistoryService mChangeHistoryService;
		private readonly IInitializeNewBrowserCommand mInitializeNewBrowserCommand;

		private static readonly IDictionary<(Guid browserId, int serverTabId), ICollection<Func<int, BrowserConnectionInfo, Task>>>
			mActionsAwaitingTabAddedAckByServerTabId = new Dictionary<(Guid browserId, int serverTabId), ICollection<Func<int, BrowserConnectionInfo, Task>>>();

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
			IDiffCalculator serverStateDiffCalculator,
			IChangeListOptimizer changeListOptimizer,
			IChangeHistoryService changeHistoryService,
			IInitializeNewBrowserCommand initializeNewBrowserCommand)
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
			mChangeListOptimizer = changeListOptimizer;
			mChangeHistoryService = changeHistoryService;
			mInitializeNewBrowserCommand = initializeNewBrowserCommand;
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
					var connectedBrowsers = await mConnectionRepository.GetConnectedBrowsers();
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

					await ExecuteQueuedActionsForNotAcknowledgedYetTab(
						requestData.BrowserId,
						tabId,
						requestData.ServerTabId);

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

		private async Task ExecuteQueuedActionsForNotAcknowledgedYetTab(
			Guid browserId,
			int browserTabId,
			int serverTabId)
		{
			var key = (browserId, serverTabId);
			if (mActionsAwaitingTabAddedAckByServerTabId.TryGetValue(key, out var actionsToExecute))
			{
				var browserConnectionInfo = await mConnectionRepository.GetByBrowserId(browserId);
				foreach (var action in actionsToExecute)
				{
					await action(browserTabId, browserConnectionInfo);
				}

				mActionsAwaitingTabAddedAckByServerTabId.Remove(key);
			}
		}

		public async Task MoveTab(Guid browserId, int tabId, int newIndex, bool isAck = false)
		{
			mLogger.LogInformation($"Moving a tab {tabId} at {newIndex}, IsAck = {isAck}.");

			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var movedServerTab = await mTabService.MoveTab(browserId, tabId, newIndex);

					if (!isAck)
					{
						await ForEveryOtherConnectedBrowserWithTab(browserId, movedServerTab.Id,
							async (browserTabId, connectionInfo) =>
							{
								await Clients.Client(connectionInfo.ConnectionId).MoveTab(browserTabId, newIndex);
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

		public async Task ChangeTabUrl(Guid browserId, int tabId, string newUrl, bool isAck = false)
		{
			mLogger.LogInformation($"Changing tab {tabId} url to {newUrl}, IsAck = {isAck}.");

			await @lock.WaitAsync();
			try
			{
				using (var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					var changedServerTab = await mTabService.ChangeTabUrl(browserId, tabId, newUrl);
					if (changedServerTab != null && !isAck)
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

		public async Task ActivateTab(Guid browserId, int tabId, bool isAck = false)
		{
			mLogger.LogInformation($"Activating tab {tabId}, IsAck = {isAck}.");

			if (isAck)
			{
				return;
			}

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

		public async Task<bool> DoesSynchronizeNeedUrls(Guid browserId)
		{
			await @lock.WaitAsync();
			try
			{
				var browser = await mBrowserRepository.GetById(browserId);

				// Tabs' urls are needed only on first initialization.
				// Retrieval of urls on Firefox Android with many tabs is 
				// very costly - 2-3 minutes for 150-200 tabs and the browser
				// is useless in meantime.
				return browser == null;
			}
			finally
			{
				@lock.Release();
			}
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
						var browserInfo = new Browser
						{
							Id = browserId,
							Name = Context.Request.Headers["User-Agent"]
						};

						await mInitializeNewBrowserCommand.ExecuteAsync(Clients.Caller, browserInfo, currentlyOpenTabs);
					}
					else
					{
						var browserChanges = changesSinceLastConnection.Select(mTabActionDeserializer.Deserialize).ToList();

						mLogger.LogDebug(
							$"Synchronize(browserId: ({browserId}), " +
							$"browserChanges: " + Environment.NewLine +
							$"{String.Join(";\n", browserChanges)}, " + Environment.NewLine +
							$"currentlyOpenTabs: " + Environment.NewLine +
							$"{String.Join(";\n", currentlyOpenTabs)})");

						browserChanges = mChangeHistoryService.FilterOutAlreadyProcessedChanges(browserId, browserChanges).ToList();
						browserChanges = mChangeListOptimizer.GetOptimizedList(browserChanges).ToList();

						mLogger.LogDebug(
							$"Changes have been optimized to: " + Environment.NewLine +
							$"{String.Join(";\n", browserChanges)}");

						var browserStateOnLastUpdate = (await mBrowserTabRepository.GetAllBrowsersTabs(browserId)).ToList();

						mLogger.LogDebug(
							$"Browser state on server: " + Environment.NewLine +
							$"{String.Join(";\n", browserStateOnLastUpdate)}");

						UpdateBrowserTabIdMapping(browserStateOnLastUpdate, currentlyOpenTabs, browserChanges);

						await mUoW.SaveChangesAsync();

						await ApplyChangesFromServerToBrowser(browserId, browserStateOnLastUpdate);

						// TODO Extract into TabActionToHubMethodsDispatcher if it will work nicely with conflicts
						var oldIdsByIndex = browserStateOnLastUpdate.ToDictionary(x => x.Index, x => x.BrowserTabId);
						var newIdsByIndex = currentlyOpenTabs.ToDictionary(x => x.Index.Value, x => x.Id);
						foreach (var change in browserChanges)
						{
							var tabId = GetTabIdOfChangedTab(change, browserChanges, oldIdsByIndex, newIdsByIndex);

							switch (change)
							{
								case TabCreatedDto dto:
									var serverTab = await mTabService.AddTab(browserId, tabId, dto.TabIndex, dto.Url, dto.CreateInBackground);

									// Again... EF is annoying with it's ids... switch over to guids?
									await mUoW.SaveChangesAsync(); // Retrieve automatically assigned ids from database

									var connectedBrowsers = await mConnectionRepository.GetConnectedBrowsers();
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
												await Clients.Client(connectionInfo.ConnectionId).ChangeTabUrl(browserTabId, dto.NewUrl);
											});
									}
									break;

								case TabMovedDto dto:
									var movedServerTab = await mTabService.MoveTab(browserId, tabId, dto.NewIndex);

									await ForEveryOtherConnectedBrowserWithTab(browserId, movedServerTab.Id,
										async (browserTabId, connectionInfo) =>
										{
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
					tabOnServer.BrowserTabId = -tabOnServer.BrowserTabId - 1;
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

			mLogger.LogDebug(
				$"Changes to apply from the server: " + Environment.NewLine +
				$"{String.Join(";\n", serverSideChanges)}" + Environment.NewLine +
				$"Browser state: " + Environment.NewLine +
				$"{String.Join(";\n", browserStateOnLastUpdate)}" + Environment.NewLine +
				$"Server state: " + Environment.NewLine +
				$"{String.Join(";\n", serverState)}");

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
						addTabAction.CreateInBackground,
						isRequestedByInitializer: true);

					continue;
				}

				var browserTabId = GetTabIdOfChangedTab(change, serverSideChanges, idsByOldIndex, idsByNewIndex);

				switch (change)
				{
					case TabUrlChangedDto dto:
						await Clients.Caller.ChangeTabUrl(browserTabId, dto.NewUrl, isRequestedByInitializer: true);
						break;

					case TabMovedDto dto:
						await Clients.Caller.MoveTab(browserTabId, dto.NewIndex, isRequestedByInitializer: true);
						break;

					case TabClosedDto dto:
						await Clients.Caller.CloseTab(browserTabId, isRequestedByInitializer: true);
						break;
				}
			}
		}

		private int GetTabIdOfChangedTab(
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

			var previousIndex = mIndexCalculator.GetTabIndexBeforeChanges(change.TabIndex, allChanges.TakeWhile(x => x != change));
			if (change is TabCreatedDto || previousIndex == null)
			{
				// The tab has been added and removed before synchronization. It should be optimized out by the change optimizer.
				throw new InvalidOperationException("This should not happen! GetTabIdOfChangedTab got tab created and removed in the same session. Optimizer seems" +
																						"to not be working correctly.");
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
			var connectedBrowsers = await mConnectionRepository.GetConnectedBrowsers();
			foreach (var browser in connectedBrowsers.Where(x => x.BrowserId != currentBrowserId))
			{
				var browserTabId = await mTabIdMapper.GetBrowserTabIdForServerTabId(browser.BrowserId, serverTabId);
				if (browserTabId != null)
				{
					await action(browserTabId.Value, browser);
					continue;
				}

				if (mPendingRequestService.IsThereAPendingAddTabRequestForServerTab(browser.BrowserId, serverTabId))
				{
					mLogger.LogDebug($"Tab with id = {serverTabId} is awaiting an acknowledge on browser {browser.BrowserId}.");

					var key = (browser.BrowserId, serverTabId);
					if (!mActionsAwaitingTabAddedAckByServerTabId.TryGetValue(key, out var queuedActions))
					{
						queuedActions = new List<Func<int, BrowserConnectionInfo, Task>>();
						mActionsAwaitingTabAddedAckByServerTabId[key] = queuedActions;
					}

					queuedActions.Add(action);
					continue;
				}

				mLogger.LogDebug($"Browser {browser.BrowserId} does not have a tab with id = {serverTabId}.");
			}
		}
	}
}