using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

public class SynchronizerHub : Hub
{
	private readonly ILogger mLogger;

	public SynchronizerHub(ILogger<SynchronizerHub> logger)
	{
		mLogger = logger;
	}

	public void AddTab()
	{
		mLogger.LogInformation("Adding a new tab.");

		Clients.Others.appendEmptyTab();

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
}