using System.Collections.Generic;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
    public interface IChangeListOptimizer
    {
        IEnumerable<TabAction> GetOptimizedList(IReadOnlyCollection<TabAction> changesToOptimize);
    }
}