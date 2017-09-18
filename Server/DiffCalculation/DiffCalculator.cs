using System.Collections.Generic;
using System.Linq;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
    public class DiffCalculator : IDiffCalculator
    {
        public IEnumerable<TabAction> ComputeChanges(
            IReadOnlyCollection<BrowserTab> original,
            IReadOnlyCollection<TabData> changed)
        {
            var addedTabs = changed.Where(x => original.All(y => !x.Equals(y.ServerTab))).Where(x => x.IsOpen);
            var addActions = addedTabs.Select(x => new TabCreatedDto
            {
                CreateInBackground = true,
                TabIndex = x.Index.Value,
                Url = x.Url
            });
            
            var closedTabs = original.Where(x => !x.ServerTab.IsOpen);
            var closeActions = closedTabs.Select(x => new TabClosedDto
            {
                TabIndex = x.Index
            });
            
            return addActions.Concat<TabAction>(closeActions);
        }
    }
}