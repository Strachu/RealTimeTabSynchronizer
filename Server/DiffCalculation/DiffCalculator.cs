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
            var addActions = GetAddActions(original, changed).ToList();
            var closeActions = GetCloseActions(original).ToList();
            var moveActions = GetMoveActions(original, addActions, closeActions);

            return addActions.Concat<TabAction>(closeActions).Concat(moveActions);
        }

        private IEnumerable<TabCreatedDto> GetAddActions(IReadOnlyCollection<BrowserTab> original, IReadOnlyCollection<TabData> changed)
        {
            var addedTabs = changed.Where(x => original.All(y => !x.Equals(y.ServerTab))).Where(x => x.IsOpen);
            return addedTabs.Select(x => new TabCreatedDto
            {
                CreateInBackground = true,
                TabIndex = x.Index.Value,
                Url = x.Url
            });
        }

        private IEnumerable<TabClosedDto> GetCloseActions(IReadOnlyCollection<BrowserTab> original)
        {
            var closedTabs = original.Where(x => !x.ServerTab.IsOpen).ToList();
            return closedTabs.Select(x => new TabClosedDto
            {
                TabIndex = x.Index
            });
        }

        private IEnumerable<TabMovedDto> GetMoveActions(
            IEnumerable<BrowserTab> original,
            IEnumerable<TabCreatedDto> addActions,
            IEnumerable<TabClosedDto> closeActions)
        {
            var movedTabs = original
                .Where(x => x.ServerTab.IsOpen)
                .Select(x => new TabWithAdjustedIndices
                {
                    Tab = x,
                    OriginalIndex = x.Index,
                    NewIndex = x.ServerTab.Index ?? -1
                }).ToList();

            foreach (var addAction in addActions)
            {
                foreach (var tab in movedTabs.Where(x => x.OriginalIndex >= addAction.TabIndex))
                {
                    tab.OriginalIndex++;
                }
            }
 
            foreach (var closeAction in closeActions)
            {
                foreach (var tab in movedTabs.Where(x => x.OriginalIndex > closeAction.TabIndex))
                {
                    tab.OriginalIndex--;
                }
            }
            
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
            public BrowserTab Tab { get; set; }
            public int OriginalIndex { get; set; }
            public int NewIndex { get; set; }

            public int Difference => NewIndex - OriginalIndex;
        }
    }
}