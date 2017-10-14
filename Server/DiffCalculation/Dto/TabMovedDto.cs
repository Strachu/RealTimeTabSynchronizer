namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public class TabMovedDto : TabAction
	{
		public int NewIndex { get; set; }

		public override string ToString()
		{
			return $"[TabMoved]: {base.ToString()}, {nameof(NewIndex)}: {NewIndex}";
		}
	}
}