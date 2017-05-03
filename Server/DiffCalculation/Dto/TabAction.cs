using System;

namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public abstract class TabAction
	{
		public int TabId { get; set; }
		public DateTime ActionTime { get; set; }
	}
}