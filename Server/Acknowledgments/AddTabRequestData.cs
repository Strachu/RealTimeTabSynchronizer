using System;

namespace RealTimeTabSynchronizer.Server.Acknowledgments
{
	public class AddTabRequestData
	{
		public Guid BrowserId { get; set; }
		public string Url { get; set; }
		public int ServerTabId { get; set; }
	}
}