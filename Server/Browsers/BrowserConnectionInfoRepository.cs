using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public class BrowserConnectionInfoRepository : IBrowserConnectionInfoRepository
	{
		private static readonly IList<BrowserConnectionInfo> mConnections = new List<BrowserConnectionInfo>();

		private readonly IBrowserRepository mBrowserRepository;

		public BrowserConnectionInfoRepository(IBrowserRepository browserRepository)
		{
			mBrowserRepository = browserRepository;
		}

		public void AddConnection(Guid browserId, string connectionId)
		{
			lock (mConnections)
			{
				var browserConnection = mConnections.SingleOrDefault(x => x.BrowserId == browserId);
				if (browserConnection == null)
				{
					mConnections.Add(new BrowserConnectionInfo
					{
						BrowserId = browserId,
						ConnectionId = connectionId
					});
				}
				else
				{
					browserConnection.ConnectionId = connectionId;
				}
			}
		}

		public void RemoveConnection(string connectionId)
		{
			lock (mConnections)
			{
				var connection = mConnections.SingleOrDefault(x => x.ConnectionId == connectionId);
				if (connection != null)
				{
					mConnections.Remove(connection);
				}
			}
		}

		public async Task<IEnumerable<BrowserConnectionInfo>> GetConnectedBrowsers()
		{
			IList<BrowserConnectionInfo> result;
			lock (mConnections)
			{
				result = mConnections.ToList();
			}

			foreach (var connection in result.ToList())
			{
				if (await mBrowserRepository.GetById(connection.BrowserId) == null)
				{
					result.Remove(connection);
				}
			}

			return result;
		}

		public async Task<BrowserConnectionInfo> GetByBrowserId(Guid browserId)
		{
			BrowserConnectionInfo connection;
			lock (mConnections)
			{
				connection = mConnections.SingleOrDefault(x => x.BrowserId == browserId);
			}

			if (connection == null)
			{
				return null;
			}

			if (await mBrowserRepository.GetById(browserId) == null)
			{
				return null;
			}

			return connection;
		}
	}
}