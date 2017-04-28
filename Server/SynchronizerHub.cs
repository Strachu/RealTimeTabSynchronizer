using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server
{
	public class SynchronizerHub : Hub
	{
		private static SemaphoreSlim @lock = new SemaphoreSlim(initialCount: 1);

		private readonly ILogger mLogger;
		private readonly TabSynchronizerDbContext mUoW;
		private readonly ITabService mTabService;
		private readonly ITabDataRepository mTabDataRepository;

		public SynchronizerHub(
			ILogger<SynchronizerHub> logger,
			TabSynchronizerDbContext dbContext,
			ITabService tabService,
			ITabDataRepository tabDataRepository)
		{
			mLogger = logger;
			mUoW = dbContext;
			mTabService = tabService;
			mTabDataRepository = tabDataRepository;
		}

		public async Task AddTab(int tabIndex, string url, bool createInBackground)
		{
			mLogger.LogInformation("Adding a new tab.");

			await @lock.WaitAsync();
			try
			{
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					await mTabService.AddTab(tabIndex, url, createInBackground);
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

		public async Task MoveTab(int oldTabIndex, int newTabIndex)
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

		public async Task CloseTab(int tabIndex)
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

		public async Task ChangeTabUrl(int tabIndex, string newUrl)
		{
			mLogger.LogInformation($"Changing tab {tabIndex} url to {newUrl}.");

			await @lock.WaitAsync();
			try
			{
				using(var transaction = await mUoW.Database.BeginTransactionAsync())
				{
					await mTabService.ChangeTabUrl(tabIndex, newUrl);
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

		public async Task ActivateTab(int tabIndex)
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

		public async Task SynchronizeTabs(IReadOnlyCollection<TabData> tabs)
		{
			mLogger.LogInformation($"Synchronizing {tabs.Count} tabs...");

			using(var transaction = await mUoW.Database.BeginTransactionAsync())
			{
				// TODO Remove duplicates??
				var existingTabs = (await mTabDataRepository.GetAllTabs()).ToList();
				var existingTabsByUrl = existingTabs.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
				var newTabs = tabs.Where(x => !existingTabsByUrl.ContainsKey(x.Url)).ToList();

				mLogger.LogDebug($"Existing tabs: {existingTabs.Count}");
				mLogger.LogDebug($"New tabs: {newTabs.Count}");

				for(int i = 0; i < newTabs.Count ;++i)
				{
					await mTabService.AddTab(existingTabs.Count + i, newTabs[i].Url, createInBackground: true);
					await Clients.Others.addTab(existingTabs.Count + i, newTabs[i].Url, createInBackground: true);
				}

				var saveChangesTask = mUoW.SaveChangesAsync();

				// TODO Optimize
				var tabsSortedByIndex = tabs.OrderBy(x => x.Index).ToArray();
				var tabsToUpdate = Math.Min(existingTabs.Count, tabs.Count);
				for(int i = 0 ; i < tabsToUpdate ;++i)
				{
					var oldTabValue = tabsSortedByIndex[i];
					var newTabValue = existingTabs[i];

					if(!oldTabValue.Url.Equals(newTabValue.Url, StringComparison.OrdinalIgnoreCase))
					{
						await Clients.Caller.changeTabUrl(i, newTabValue.Url);
					}
				}

				var allTabs = existingTabs.Concat(newTabs).ToList();
				if(allTabs.Count > tabs.Count)
				{
					for(int i = tabs.Count; i < allTabs.Count ;++i)
					{
						await Clients.Caller.addTab(i, allTabs[i].Url, createInBackground: true);
					}
				}
				else
				{
					for(int i = allTabs.Count; i < tabs.Count ;++i)
					{
						await Clients.Caller.closeTab(i);	
					}
				}
				
				await saveChangesTask;
				transaction.Commit();
			}

			mLogger.LogInformation($"Finished synchronizing tabs...");
		}
	}
}