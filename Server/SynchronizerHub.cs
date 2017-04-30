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
using Server.Dto;

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

		public SynchronizerHub(
			ILogger<SynchronizerHub> logger,
			TabSynchronizerDbContext dbContext,
			ITabService tabService,
			ITabDataRepository tabDataRepository,
			IBrowserRepository browserRepository)
		{
			mLogger = logger;
			mUoW = dbContext;
			mTabService = tabService;
			mTabDataRepository = tabDataRepository;
			mBrowserRepository = browserRepository;
		}

		public async Task AddTab(Guid browserId, int tabIndex, string url, bool createInBackground)
		{
			mLogger.LogInformation("Adding a new tab.");

			await @lock.WaitAsync();
			try
			{
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					url = await mTabService.AddTab(tabIndex, url, createInBackground);
					await mUoW.SaveChangesAsync();
					
					transaction.Commit();
				}

				await Clients.Others.addTab(tabIndex, url, createInBackground);
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished adding new tab.");
		}

		public async Task MoveTab(Guid browserId, int oldTabIndex, int newTabIndex)
		{
			mLogger.LogInformation($"Moving a tab from {oldTabIndex} to {newTabIndex}.");

			await @lock.WaitAsync();
			try
			{
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					await mTabService.MoveTab(oldTabIndex, newTabIndex);
					await mUoW.SaveChangesAsync();
					
					transaction.Commit();
				}
				
				Clients.Others.moveTab(oldTabIndex, newTabIndex);
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished moving a tab.");
		}

		public async Task CloseTab(Guid browserId, int tabIndex)
		{
			mLogger.LogInformation($"Closing a tab at index {tabIndex}.");

			await @lock.WaitAsync();
			try
			{
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					await mTabService.CloseTab(tabIndex);
					await mUoW.SaveChangesAsync();
					
					transaction.Commit();
				}

				await Clients.Others.closeTab(tabIndex);	
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished closing a tab.");
		}

		public async Task ChangeTabUrl(Guid browserId, int tabIndex, string newUrl)
		{
			mLogger.LogInformation($"Changing tab {tabIndex} url to {newUrl}.");

			await @lock.WaitAsync();
			try
			{
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					bool changed = await mTabService.ChangeTabUrl(tabIndex, newUrl);
					if(!changed)
					{
						return;
					}

					await mUoW.SaveChangesAsync();
					
					transaction.Commit();
				}

				await Clients.Others.changeTabUrl(tabIndex, newUrl);
			}
			finally
			{
				@lock.Release();
			}

			mLogger.LogInformation("Finished changing a url of tab.");
		}

		public async Task ActivateTab(Guid browserId, int tabIndex)
		{
			mLogger.LogInformation($"Activating tab {tabIndex}.");

			await @lock.WaitAsync();
			try
			{
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					await mTabService.ActivateTab(tabIndex);
					await mUoW.SaveChangesAsync();
					
					transaction.Commit();
				}
				
				await Clients.Others.activateTab(tabIndex);
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
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					mLogger.LogInformation($"[{browserId}]: Synchronizing {changesSinceLastConnection.Count} changes " + 
						$"with currently {currentlyOpenTabs.Count} open tabs...");

					var browser = await mBrowserRepository.GetById(browserId);
					if(browser == null)
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

						for(int i = 0; i < newTabs.Count ;++i)
						{
							await mTabService.AddTab(tabsAlreadyOnServer.Count + i, newTabs[i].Url, createInBackground: true);
							await Clients.Others.addTab(tabsAlreadyOnServer.Count + i, newTabs[i].Url, createInBackground: true);
						}

						var tabsSortedByIndex = currentlyOpenTabs.OrderBy(x => x.Index).ToList();
						var tabsToUpdate = Math.Min(tabsAlreadyOnServer.Count, tabsSortedByIndex.Count);
						for(int i = 0 ; i < tabsToUpdate ;++i)
						{
							var oldTabValue = tabsSortedByIndex[i];
							var newTabValue = tabsAlreadyOnServer[i];

							if(!oldTabValue.Url.Equals(newTabValue.Url, StringComparison.OrdinalIgnoreCase))
							{
								await Clients.Caller.changeTabUrl(i, newTabValue.Url);
							}
						}

						var allTabs = tabsAlreadyOnServer.Concat(newTabs).ToList();
						if(allTabs.Count > newTabs.Count)
						{
							for(int i = currentlyOpenTabs.Count; i < allTabs.Count ;++i)
							{
								await Clients.Caller.addTab(i, allTabs[i].Url, createInBackground: true);
							}
						}
						else
						{
							// TODO This always removes last tabs, it should check ids of tabs to remove.
							// Or maybe do not remove duplicates?
							for(int i = allTabs.Count; i < currentlyOpenTabs.Count ;++i)
							{
								await Clients.Caller.closeTab(i);	
							}
						}

						// TODO Initialize Tab Id -> Index -> Internal Id Mapping
						// TODO Initialize browser state
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
	}
}