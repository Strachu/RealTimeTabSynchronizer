namespace RealTimeTabSynchronizer.Server.DiffCalculation.Dto
{
	public class TabCreatedDto : TabAction
	{
		public string Url { get; set; }
		public bool CreateInBackground { get; set; }
	}
}