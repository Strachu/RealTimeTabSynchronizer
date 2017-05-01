using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public class BrowserConnectionInfoRepository : IBrowserConnectionInfoRepository
	{
		private readonly ConcurrentDictionary<string, BrowserConnectionInfo> mConnectionInfoByConnectionId =
			new ConcurrentDictionary<string, BrowserConnectionInfo>();

		public void AddConnection(Guid browserId, string connectionId)
		{
			var connectionInfo = new BrowserConnectionInfo
			{
				BrowserId = browserId,
				ConnectionId = connectionId
			};

			mConnectionInfoByConnectionId.TryAdd(connectionId, connectionInfo);
		}

		public void RemoveConnection(string connectionId)
		{
			mConnectionInfoByConnectionId.TryRemove(connectionId, out _);
		}

		public IEnumerable<BrowserConnectionInfo> GetConnectedBrowsers()
		{
			return mConnectionInfoByConnectionId.Values;
		}
	}
}