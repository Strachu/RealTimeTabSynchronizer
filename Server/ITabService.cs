using System;
using System.Threading.Tasks;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server
{
	public interface ITabService
	{
		Task<TabData> AddTab(int tabIndex, string url, bool createInBackground);
		Task<TabData> AddTab(Guid browserId, int tabId, int tabIndex, string url, bool createInBackground);
		Task<TabData> MoveTab(Guid browserId, int tabId, int newTabIndex);
		Task<TabData> CloseTab(Guid browserId, int tabId);
		Task<TabData> ChangeTabUrl(Guid browserId, int tabId, string newUrl);
		Task<TabData> ActivateTab(Guid browserId, int tabId);
	}
}