using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public class BrowserConnectionInfoRepository : IBrowserConnectionInfoRepository
	{
		private readonly ConcurrentDictionary<string, BrowserConnectionInfo> mConnectionInfoByConnectionId =
			new ConcurrentDictionary<string, BrowserConnectionInfo>();

		private readonly IBrowserRepository mBrowserRepository;

		public BrowserConnectionInfoRepository(IBrowserRepository browserRepository)
		{
			mBrowserRepository = browserRepository;
		}

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

		public async Task<IEnumerable<BrowserConnectionInfo>> GetConnectedBrowsers()
		{
			var temp = mConnectionInfoByConnectionId.Values
				.Select(x => new { Value = x, Browser = mBrowserRepository.GetById(x.BrowserId) });

			await Task.WhenAll(temp.Select(x => x.Browser));

			return temp.Where(x => x.Browser.Result != null).Select(x => x.Value);
		}

		public async Task<BrowserConnectionInfo> GetByBrowserId(Guid browserId)
		{
			var connection = mConnectionInfoByConnectionId.Values.SingleOrDefault(x => x.BrowserId == browserId);
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