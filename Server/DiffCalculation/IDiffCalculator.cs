using System.Collections.Generic;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
    public interface IDiffCalculator
    {
        IEnumerable<TabAction> ComputeChanges(
            IReadOnlyCollection<BrowserTab> original,
            IReadOnlyCollection<TabData> changed);
    }
}