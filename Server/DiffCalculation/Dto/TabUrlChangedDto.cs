namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public class TabUrlChangedDto : TabAction
	{
		public string NewUrl { get; set; }

		public override string ToString()
		{
			return $"[TabUrlChanged]: {base.ToString()}, {nameof(NewUrl)}: {NewUrl}";
		}
	}
}