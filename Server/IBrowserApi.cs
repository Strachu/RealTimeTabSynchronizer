using System;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server
{
	public interface IBrowserApi
	{
		Task AddTab(Guid requestId, int index, string url, bool createInBackground);
		Task MoveTab(int tabId, int moveToIndex);
		Task CloseTab(int tabId);
		Task ChangeTabUrl(int tabId, string newUrl);
		Task ActivateTab(int tabId);
	}
}