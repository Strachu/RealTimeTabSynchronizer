namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public class TabClosedDto : TabAction
	{
		public override string ToString()
		{
			return $"[TabClosed]: {base.ToString()}";
		}
	}
}