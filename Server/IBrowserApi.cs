using System;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server
{
	public interface IBrowserApi
	{
		Task AddTab(Guid requestId, int index, string url, bool createInBackground, bool isRequestedByInitializer = false);
		Task MoveTab(int tabId, int moveToIndex, bool isRequestedByInitializer = false);
		Task CloseTab(int tabId, bool isRequestedByInitializer = false);
		Task ChangeTabUrl(int tabId, string newUrl, bool isRequestedByInitializer = false);
		Task ActivateTab(int tabId, bool isRequestedByInitializer = false);
	}
}