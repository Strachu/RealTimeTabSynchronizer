using System;

namespace RealTimeTabSynchronizer.Server.Acknowledgments
{
	public class PendingRequest
	{
		public PendingRequest()
		{
			Id = Guid.NewGuid();
			CreationTimeUtc = DateTime.UtcNow;
		}

		public Guid Id { get; set; }
		public DateTime CreationTimeUtc { get; set; }
		public string SerializedData { get; set; }
	}
}