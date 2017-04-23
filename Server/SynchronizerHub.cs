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

	public async Task AddTab(int tabIndex, string url, bool createInBackground)
	{
		mLogger.LogInformation("Adding a new tab.");

		if(url.Equals("about:newtab", StringComparison.OrdinalIgnoreCase))
		{
			// TODO this should be done in firefox addon as it is specific to browser.
			url = "about:blank"; // Firefox for android ignores tabs with "about:newtab".
		}

		mTabDataRepository.Add(new TabData { Index = tabIndex, Url = url });

		await Clients.Others.addTab(tabIndex, url, createInBackground);

		await mUoW.SaveChangesAsync();

		mLogger.LogInformation("Finished adding new tab.");
	}

	public void MoveTab(int oldTabIndex, int newTabIndex)
	{
		mLogger.LogInformation($"Moving a tab from {oldTabIndex} to {newTabIndex}.");

		Clients.Others.moveTab(oldTabIndex, newTabIndex);

		mLogger.LogInformation("Finished moving a tab.");
	}

	public async Task CloseTab(int tabIndex)
	{
		mLogger.LogInformation($"Closing a tab at index {tabIndex}.");

		var tab = await mTabDataRepository.GetByIndex(tabIndex);

		await Clients.Others.closeTab(tab.Index);		

		mTabDataRepository.Remove(tab);
		await mUoW.SaveChangesAsync();	

		mLogger.LogInformation("Finished closing a tab.");
	}

	public async Task ChangeTabUrl(int tabIndex, string newUrl)
	{
		mLogger.LogInformation($"Changing tab {tabIndex} url to {newUrl}.");

		var tab = await mTabDataRepository.GetByIndex(tabIndex);
		if(tab.Url.Equals(newUrl, StringComparison.OrdinalIgnoreCase))
		{	
			mLogger.LogDebug($"The url did not change.");
			return;
		}

		tab.Url = newUrl;

		await Clients.Others.changeTabUrl(tab.Index, tab.Url);
		await mUoW.SaveChangesAsync();			
		
		mLogger.LogInformation("Finished changing a url of tab.");
	}

	// TODO Firefox for Android returns about:blank when page has not been loaded...
	public async Task SynchronizeTabs(IReadOnlyCollection<TabData> tabs)
	{
		mLogger.LogInformation($"Synchronizing {tabs.Count} tabs...");

		// TODO Remove duplicates??
		var existingTabs = (await mTabDataRepository.GetAllTabs()).Take(500).ToList();
		var existingTabsByUrl = existingTabs.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
		var newTabs = tabs.Where(x => !existingTabsByUrl.ContainsKey(x.Url)).ToList();

		mLogger.LogDebug($"Existing tabs: {existingTabs.Count}");
		mLogger.LogDebug($"New tabs: {newTabs.Count}");

		for(int i = 0; i < newTabs.Count ;++i)
		{
			// TODO Add tab to others...
			var tab = new TabData
			{
				Index = existingTabs.Count + i,
				Url = newTabs[i].Url
			};
			mTabDataRepository.Add(tab);
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

		mLogger.LogInformation($"Finished synchronizing tabs...");
	}
}