using System.Collections.Generic;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.TabData_
{
	public interface ITabDataRepository
	{
		void Add(TabData tab);
		Task<IEnumerable<TabData>> GetAllTabs();
		Task<TabData> GetByIndex(int index);
		Task<int> GetTabCount();
		void Remove(TabData tab);

		Task IncrementTabIndices(TabRange range, int incrementBy);
	}
}