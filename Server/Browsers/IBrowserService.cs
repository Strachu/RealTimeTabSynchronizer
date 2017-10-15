using System;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public interface IBrowserService
	{
		Task AddTab(
			Guid browserId,
			int serverTabId,
			int index,
			string url,
			bool createInBackground,
			bool isRequestedByInitializer = false);
	}
}