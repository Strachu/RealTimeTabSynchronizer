using System;

namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public abstract class TabAction
	{
		public Guid ActionId { get; set; }
		
		// Reference has to be done by index due to some browsers changing the ids every run,
		// without index it would be impossible to correlate tabs on server and on the browser.
		public int TabIndex { get; set; }
		public DateTime ActionTime { get; set; }

		public virtual TabAction Clone()
		{
			return (TabAction)base.MemberwiseClone();
		}

		public override string ToString()
		{
			return $"{nameof(TabIndex)}: {TabIndex}";
		}
	}
}