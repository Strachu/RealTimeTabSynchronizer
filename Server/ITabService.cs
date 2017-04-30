using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server
{
	public interface ITabService
	{
		Task<string> AddTab(int tabIndex, string url, bool createInBackground);
		Task MoveTab(int oldTabIndex, int newTabIndex);
		Task CloseTab(int tabIndex);
		Task<bool> ChangeTabUrl(int tabIndex, string newUrl);
		Task ActivateTab(int tabIndex);
	}
}