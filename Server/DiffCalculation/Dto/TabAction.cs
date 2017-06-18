using System;

namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public abstract class TabAction
	{
		// Reference has to be done by index due to some browsers changing the ids every run,
		// without index it would be impossible to correlate tabs on server and on the browser.
		public int TabIndex { get; set; }
		public DateTime ActionTime { get; set; }
	}
}