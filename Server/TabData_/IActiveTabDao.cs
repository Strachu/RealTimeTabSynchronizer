using System.Collections.Generic;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.TabData_
{
	public interface IActiveTabDao
	{
		Task<TabData> GetActiveTab();
		Task SetActiveTab(TabData tab);
	}
}