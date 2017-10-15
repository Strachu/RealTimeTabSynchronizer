using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public class BrowserConnectionInfoRepository : IBrowserConnectionInfoRepository
	{
		private readonly IList<BrowserConnectionInfo> mConnections = new List<BrowserConnectionInfo>();

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
			var temp = mConnections.Select(x => new { Value = x, Browser = mBrowserRepository.GetById(x.BrowserId) });

			await Task.WhenAll(temp.Select(x => x.Browser));

			return temp.Where(x => x.Browser.Result != null).Select(x => x.Value);
		}

		public async Task<BrowserConnectionInfo> GetByBrowserId(Guid browserId)
		{
			var connection = mConnections.SingleOrDefault(x => x.BrowserId == browserId);
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