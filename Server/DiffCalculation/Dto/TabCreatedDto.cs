namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public class TabCreatedDto : TabAction
	{
		public int Index { get; set; }
		public string Url { get; set; }
		public bool CreateInBackground { get; set; }
	}
}