using System.Collections.Generic;
using System.Threading.Tasks;

public interface ITabDataRepository
{
	void AddTab(TabData tab);
	Task<IEnumerable<TabData>> GetAllTabs();
	Task<int> GetTabCount();
}