using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public interface IBrowserConnectionInfoRepository
	{
		void AddConnection(Guid browserId, string connectionId);
		void RemoveConnection(string connectionId);

		Task<IEnumerable<BrowserConnectionInfo>> GetConnectedBrowsers();
		Task<BrowserConnectionInfo> GetByBrowserId(Guid browserId);
	}
}