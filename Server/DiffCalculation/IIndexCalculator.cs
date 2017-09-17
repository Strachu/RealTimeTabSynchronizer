using System.Collections.Generic;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
    public interface IIndexCalculator
    {
        int? GetTabIndexAfterChanges(int currentIndex, IEnumerable<TabAction> changes);
        int? GetTabIndexBeforeChanges(int newIndex, IEnumerable<TabAction> changes);
    }
}