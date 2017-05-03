namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public class TabMovedDto : TabAction
	{
		public int OldIndex { get; set; } // TODO This seems to be not need at this time, is it correct?
		public int NewIndex { get; set; }
	}
}