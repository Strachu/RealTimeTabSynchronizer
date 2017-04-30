using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public interface IBrowserRepository
	{
		void Add(Browser tab);
		Task<Browser> GetById(Guid id);
	}
}