using System.Collections.Generic;
using System.Threading.Tasks;

public interface ITabDataRepository
{
	void Add(TabData tab);
	Task<IEnumerable<TabData>> GetAllTabs();
	Task<TabData> GetByIndex(int index);
	Task<int> GetTabCount();
	void Remove(TabData tab);
}