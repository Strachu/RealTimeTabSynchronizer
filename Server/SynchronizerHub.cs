using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

public class SynchronizerHub : Hub
{
	private readonly ILogger mLogger;
	private readonly TabSynchronizerDbContext mUoW;
	private readonly ITabDataRepository mTabDataRepository;

	public SynchronizerHub(
		ILogger<SynchronizerHub> logger,
		TabSynchronizerDbContext dbContext,
		ITabDataRepository tabDataRepository)
	{
		mLogger = logger;
		mUoW = dbContext;
		mTabDataRepository = tabDataRepository;
	}

	public async Task AddTab()
	{
		mLogger.LogInformation("Adding a new tab.");

		var tabIndex = await mTabDataRepository.GetTabCount();

		mTabDataRepository.AddTab(new TabData { Index = tabIndex, Url = "about:blank" });

		await Clients.Others.appendEmptyTab();

		await mUoW.SaveChangesAsync();

		mLogger.LogInformation("Finished adding new tab.");
	}

	public void MoveTab(int oldTabIndex, int newTabIndex)
	{
		mLogger.LogInformation($"Moving a tab from {oldTabIndex} to {newTabIndex}.");

		Clients.Others.moveTab(oldTabIndex, newTabIndex);

		mLogger.LogInformation("Finished moving a tab.");
	}

	public void CloseTab(int tabIndex)
	{
		mLogger.LogInformation($"Closing a tab at index {tabIndex}.");

		Clients.Others.closeTab(tabIndex);		

		mLogger.LogInformation("Finished moving a tab.");
	}

	public void ChangeTabUrl(int tabIndex, string newUrl)
	{
		mLogger.LogInformation($"Changing tab {tabIndex} url to {newUrl}.");

		Clients.Others.changeTabUrl(tabIndex, newUrl);				

		mLogger.LogInformation("Finished changing a url of tab.");
	}

	public async Task SynchronizeTabs(IReadOnlyCollection<TabData> tabs)
	{
		mLogger.LogInformation($"Synchronizing tabs...");

		// TODO Remove duplicates??
		var existingTabs = (await mTabDataRepository.GetAllTabs()).ToList();
		var existingTabsByUrl = existingTabs.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
		var newTabs = tabs.Where(x => !existingTabsByUrl.ContainsKey(x.Url)).ToList();

		for(int i = 0; i < newTabs.Count ;++i)
		{
			// TODO Add tab to others...
			var tab = new TabData
			{
				Index = existingTabs.Count + i,
				Url = newTabs[i].Url
			};
			mTabDataRepository.AddTab(tab);
		}

		var saveChangesTask = mUoW.SaveChangesAsync();

		// TODO Optimize
		var allTabs = existingTabs.Concat(newTabs).ToList();
		var tabsToCreate = newTabs.Count + existingTabs.Count - tabs.Count;
		for(int i = 0; i < tabsToCreate ;++i)
		{
			await Clients.Caller.appendEmptyTab();
		}

		var tabsSortedByIndex = tabs.OrderBy(x => x.Index).ToArray();
		for(int i = 0 ; i < allTabs.Count ;++i)
		{
			var newTabValue = allTabs[i];

			bool update = true;
			if(i < tabsSortedByIndex.Length)
			{
				var oldTabValue = tabsSortedByIndex[i];

				update = !oldTabValue.Url.Equals(newTabValue.Url, StringComparison.OrdinalIgnoreCase);
			}

			if(update)
			{
				await Clients.Caller.changeTabUrl(i, newTabValue.Url);
			}
		}
		
		await saveChangesTask;

		mLogger.LogInformation($"Finished synchronizing tabs...");
	}
}