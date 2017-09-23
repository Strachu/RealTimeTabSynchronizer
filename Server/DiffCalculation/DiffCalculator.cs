using System;
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
            var originalTabsWithAdjustedIndices = original.Select(x => new TabWithAdjustedIndices(x)).ToList();
            
            var addActions = GetAddActions(originalTabsWithAdjustedIndices, changed);
            var closeActions = GetCloseActions(originalTabsWithAdjustedIndices);
            var moveActions = GetMoveActions(originalTabsWithAdjustedIndices);

            return addActions.Concat<TabAction>(closeActions).Concat(moveActions);
        }

        private IEnumerable<TabCreatedDto> GetAddActions(
            IReadOnlyCollection<TabWithAdjustedIndices> original,
            IReadOnlyCollection<TabData> changed)
        {
            var addedTabs = changed.Where(x => original.All(y => !x.Equals(y.Tab.ServerTab))).Where(x => x.IsOpen);
            var addActions = addedTabs.Select(x => new TabCreatedDto
                {
                    CreateInBackground = true,
                    TabIndex = x.Index.Value,
                    Url = x.Url
                })
                .OrderBy(x => x.TabIndex)
                .ToList();
            
            foreach (var action in addActions)
            {
                foreach (var tab in original.Where(x => x.OriginalIndex >= action.TabIndex))
                {
                    tab.OriginalIndex++;
                }
            }
            
            return addActions;
        }

        private IEnumerable<TabClosedDto> GetCloseActions(IReadOnlyCollection<TabWithAdjustedIndices> original)
        {
            var closedTabs = original.Where(x => !x.Tab.ServerTab.IsOpen).ToList();
            var closeActions = new List<TabClosedDto>();

            foreach (var closedTab in closedTabs)
            {
                closeActions.Add(new TabClosedDto
                {
                    TabIndex = closedTab.OriginalIndex
                });
                
                foreach (var tab in original.Where(x => x.OriginalIndex > closedTab.OriginalIndex))
                {
                    tab.OriginalIndex--;
                }
            }       
            
            return closeActions;
        }

        private IEnumerable<TabMovedDto> GetMoveActions(IEnumerable<TabWithAdjustedIndices> original)
        {
            var movedTabs = original.Where(x => x.Tab.ServerTab.IsOpen).ToList();
            var moveActions = new List<TabMovedDto>();

            while (movedTabs.Any(x => x.Difference != 0))
            {
                var tabWithBiggestDiff = movedTabs.OrderByDescending(x => Math.Abs(x.Difference)).FirstOrDefault();
                if (tabWithBiggestDiff != null)
                {
                    if (IsMovedForward(tabWithBiggestDiff, movedTabs))
                    {
                        moveActions.Add(new TabMovedDto
                        {
                            TabIndex = tabWithBiggestDiff.OriginalIndex,
                            NewIndex = tabWithBiggestDiff.NewIndex
                        });

                        var tabsInRange = movedTabs.Where(x => x.OriginalIndex > tabWithBiggestDiff.OriginalIndex && 
                                                               x.OriginalIndex <= tabWithBiggestDiff.NewIndex);
                        foreach (var tab in tabsInRange)
                        {
                            tab.OriginalIndex--;
                        }
                        
                        tabWithBiggestDiff.OriginalIndex = tabWithBiggestDiff.NewIndex;
                    }
                    else if (IsMovedBackwards(tabWithBiggestDiff, movedTabs))
                    {
                        moveActions.Add(new TabMovedDto
                        {
                            TabIndex = tabWithBiggestDiff.OriginalIndex,
                            NewIndex = tabWithBiggestDiff.NewIndex
                        });

                        var tabsInRange = movedTabs.Where(x => x.OriginalIndex < tabWithBiggestDiff.OriginalIndex && 
                                                               x.OriginalIndex >= tabWithBiggestDiff.NewIndex);
                        foreach (var tab in tabsInRange)
                        {
                            tab.OriginalIndex++;
                        }
                        
                        tabWithBiggestDiff.OriginalIndex = tabWithBiggestDiff.NewIndex;
                    }
                }
            }
            
            return moveActions;
        }

        private bool IsMovedForward(TabWithAdjustedIndices tab, IEnumerable<TabWithAdjustedIndices> allTabs)
        {
            var tabsInRange = allTabs.Where(x => x.OriginalIndex > tab.OriginalIndex && x.OriginalIndex <= tab.NewIndex);
            
            return tab.Difference > 0 && tabsInRange.All(x => x.Difference < tab.Difference);
        }
        
        private bool IsMovedBackwards(TabWithAdjustedIndices tab, IEnumerable<TabWithAdjustedIndices> allTabs)
        {
            var tabsInRange = allTabs.Where(x => x.OriginalIndex < tab.OriginalIndex && x.OriginalIndex >= tab.NewIndex);
            
            return tab.Difference < 0 && tabsInRange.All(x => x.Difference > tab.Difference);
        }
        
        private class TabWithAdjustedIndices
        {
            public TabWithAdjustedIndices(BrowserTab tab)
            {
                Tab = tab;
                OriginalIndex = tab.Index;
            }
            
            public BrowserTab Tab { get; set; }
            public int OriginalIndex { get; set; }

            public int NewIndex => Tab.ServerTab.Index.Value;
            public int Difference => NewIndex - OriginalIndex;
        }
    }
}