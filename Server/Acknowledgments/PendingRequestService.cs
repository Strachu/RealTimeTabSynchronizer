using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RealTimeTabSynchronizer.Server.EntityFramework;

namespace RealTimeTabSynchronizer.Server.Acknowledgments
{
	public class PendingRequestService : IPendingRequestService
	{
		private readonly TabSynchronizerDbContext mContext;

		public PendingRequestService(TabSynchronizerDbContext context)
		{
			mContext = context;
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
		}

		public async Task<T> GetRequestDataByPendingRequestId<T>(Guid requestId)
		{
			var request = await mContext.PendingRequests.FindAsync(requestId);

			return JsonConvert.DeserializeObject<T>(request.SerializedData);
		}
	}
}