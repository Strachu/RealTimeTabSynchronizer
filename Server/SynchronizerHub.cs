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

		Clients.AllExcept(Clients.Caller).appendEmptyTab();

		mLogger.LogInformation("Finished adding new tab.");
	}

	public void MoveTab(int oldTabIndex, int newTabIndex)
	{
		mLogger.LogInformation($"Moving a tab from {oldTabIndex} to {newTabIndex}.");

		Clients.AllExcept(Clients.Caller).moveTab(oldTabIndex, newTabIndex);

		mLogger.LogInformation("Finished moving a tab.");
	}

	public void CloseTab(int tabIndex)
	{
		mLogger.LogInformation($"Closing a tab at index {tabIndex}.");

		Clients.AllExcept(Clients.Caller).closeTab(tabIndex);		

		mLogger.LogInformation("Finished moving a tab.");
	}

	public void ChangeTabUrl(int tabIndex, string newUrl)
	{
		mLogger.LogInformation($"Changing tab {tabIndex} url to {newUrl}.");

		Clients.AllExcept(Clients.Caller).changeTabUrl(tabIndex, newUrl);				

		mLogger.LogInformation("Finished changing a url of tab.");
	}
}