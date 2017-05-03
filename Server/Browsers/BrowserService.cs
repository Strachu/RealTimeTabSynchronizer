using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Hubs;
using RealTimeTabSynchronizer.Server;
using RealTimeTabSynchronizer.Server.Acknowledgments;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public class BrowserService : IBrowserService
	{
		private readonly IHubContext<SynchronizerHub, IBrowserApi> mCallContext;
		private readonly IBrowserConnectionInfoRepository mConnectionRepository;
		private readonly IPendingRequestService mPendingRequestService;

		public BrowserService(
			IHubContext<SynchronizerHub, IBrowserApi> signalRContext,
			IBrowserConnectionInfoRepository connectionRepository,
			IPendingRequestService pendingRequestService)
		{
			mCallContext = signalRContext;
			mConnectionRepository = connectionRepository;
			mPendingRequestService = pendingRequestService;
		}

		public async Task AddTab(Guid browserId, int serverTabId, int index, string url, bool createInBackground)
		{
			var connectionInfo = mConnectionRepository.GetByBrowserId(browserId);
			if (connectionInfo == null)
			{
				throw new InvalidOperationException($"The browser {browserId} is not connected.");
			}

			var requestData = new AddTabRequestData()
			{
				BrowserId = browserId,
				Url = url,
				ServerTabId = serverTabId
			};
			var requestId = mPendingRequestService.AddRequestAwaitingAcknowledgment(requestData);

			await mCallContext.Clients.Client(connectionInfo.ConnectionId).AddTab(requestId, index, url, createInBackground);
		}
	}
}