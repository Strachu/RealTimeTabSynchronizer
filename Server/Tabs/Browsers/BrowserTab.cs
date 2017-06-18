using System;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server.Tabs.Browsers
{
	public class BrowserTab
	{
		public int Id { get; set; }
		public int BrowserTabId { get; set; } // Not the same as Id to prevent changing PK key value.

		public Guid BrowserId { get; set; }

		public int ServerTabId { get; set; }
		public virtual TabData ServerTab { get; set; }
	}
}