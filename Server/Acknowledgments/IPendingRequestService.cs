using System;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.Acknowledgments
{
	public interface IPendingRequestService
	{
		Guid AddRequestAwaitingAcknowledgment(AddTabRequestData requestData);

		Guid AddRequestAwaitingAcknowledgment<T>(T requestData);

		void SetRequestFulfilled(Guid requestId);

		Task<T> GetRequestDataByPendingRequestId<T>(Guid requestId);

		bool IsThereAPendingAddTabRequestForServerTab(Guid browserId, int serverTabId);
	}
}