using System;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server.Tabs.Browsers
{
	public class BrowserTab
	{
		public int Id { get; set; }
		public int BrowserTabId { get; set; } // Not the same as Id to prevent changing PK key value.

		public Guid BrowserId { get; set; }

		public int Index { get; set; }
		public string Url { get; set; }

		public int ServerTabId { get; set; }
		public virtual TabData ServerTab { get; set; }

		public override string ToString()
		{
			return $"{nameof(Id)}: {Id}, {nameof(BrowserTabId)}: {BrowserTabId}, {nameof(Index)}: {Index}, {nameof(Url)}: {Url}, {nameof(ServerTabId)}: {ServerTabId}";
		}
	}
}