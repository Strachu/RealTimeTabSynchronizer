using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RealTimeTabSynchronizer.Server.EntityFramework;

namespace RealTimeTabSynchronizer.Server.Acknowledgments
{
	public class PendingRequestService : IPendingRequestService
	{
		private readonly TabSynchronizerDbContext mContext;
		private static readonly IDictionary<(Guid browserId, int serverTabId), Guid> mPendingAddTabRequestIndexByServerTabId =
			new Dictionary<(Guid browserId, int serverTabId), Guid>();

		public PendingRequestService(TabSynchronizerDbContext context)
		{
			mContext = context;
		}

		public Guid AddRequestAwaitingAcknowledgment(AddTabRequestData requestData)
		{
			var requestId = AddRequestAwaitingAcknowledgment<AddTabRequestData>(requestData);

			mPendingAddTabRequestIndexByServerTabId[(requestData.BrowserId, requestData.ServerTabId)] = requestId;
			return requestId;
		}

		public Guid AddRequestAwaitingAcknowledgment<T>(T requestData)
		{
			var request = new PendingRequest()
			{
				SerializedData = JsonConvert.SerializeObject(requestData)
			};

			mContext.Add(request);
			return request.Id;
		}

		public void SetRequestFulfilled(Guid requestId)
		{
			var request = mContext.PendingRequests.Find(requestId);

			mContext.PendingRequests.Remove(request);

			if (mPendingAddTabRequestIndexByServerTabId.Any(x => x.Value == requestId))
			{
				var indexKey = mPendingAddTabRequestIndexByServerTabId.Single(x => x.Value == requestId).Key;

				mPendingAddTabRequestIndexByServerTabId.Remove(indexKey);
			}
		}

		public async Task<T> GetRequestDataByPendingRequestId<T>(Guid requestId)
		{
			var request = await mContext.PendingRequests.FindAsync(requestId);

			return JsonConvert.DeserializeObject<T>(request.SerializedData);
		}

		public bool IsThereAPendingAddTabRequestForServerTab(Guid browserId, int serverTabId)
		{
			return mPendingAddTabRequestIndexByServerTabId.ContainsKey((browserId, serverTabId));
		}
	}
}