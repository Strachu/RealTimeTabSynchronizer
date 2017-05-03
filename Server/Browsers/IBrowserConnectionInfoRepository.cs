using System;
using System.Collections.Generic;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public interface IBrowserConnectionInfoRepository
	{
		void AddConnection(Guid browserId, string connectionId);
		void RemoveConnection(string connectionId);

		IEnumerable<BrowserConnectionInfo> GetConnectedBrowsers();
		BrowserConnectionInfo GetByBrowserId(Guid browserId);
	}
}