using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.TabData_.ClientToServerIdMapping
{
	public interface IBrowserTabIdServerTabIdMapper
	{
		Task<int?> GetBrowserTabIdForServerTabId(int serverTabId);
	}
}