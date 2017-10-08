using System;
using System.Collections.Generic;
using System.Linq;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
    public class ChangeListOptimizer : IChangeListOptimizer
    {
        private readonly IIndexCalculator mIndexCalculator;

        public ChangeListOptimizer(IIndexCalculator indexCalculator)
        {
            mIndexCalculator = indexCalculator;
        }

        public IEnumerable<TabAction> GetOptimizedList(IReadOnlyCollection<TabAction> changesToOptimize)
        {
            var optimizedChanges = changesToOptimize.Select(x => new ChangeWithState
            {
                OriginalChange = x,
                UpdatedChange = x.Clone(), 
                ShouldBeRemoved = false
            }).ToList();
            
            foreach (var change in optimizedChanges)
            {
                if (change.ShouldBeRemoved)
                {
                    continue;
                }
                
                var nextChanges = optimizedChanges.SkipWhile(x => x.OriginalChange != change.OriginalChange).Skip(1).ToList();
                switch (change.UpdatedChange)
                {
                    case TabCreatedDto tabCreated:
                    {
                        var currentIndex = change.OriginalChange.TabIndex;
                        var hasBeenRemovedCompletely = mIndexCalculator.GetTabIndexAfterChanges(currentIndex, nextChanges.Select(x => x.OriginalChange)) == null;

                        foreach (var nextChange in nextChanges)
                        {
                            if (currentIndex == nextChange.OriginalChange.TabIndex)
                            {
                                var asUrlChange = nextChange.OriginalChange as TabUrlChangedDto;
                                if (asUrlChange != null)
                                {
                                    tabCreated.Url = asUrlChange.NewUrl;
                                    nextChange.ShouldBeRemoved = true;
                                }

                                if (hasBeenRemovedCompletely)
                                {
                                    nextChange.ShouldBeRemoved = true;
                                }
                            }
                            else if(hasBeenRemovedCompletely)
                            {
                                if (currentIndex < nextChange.OriginalChange.TabIndex)
                                {
                                    nextChange.UpdatedChange.TabIndex--;
                                }
                                
                                var asMoveChange = nextChange.OriginalChange as TabMovedDto;
                                if (asMoveChange != null && (currentIndex < asMoveChange.NewIndex || (currentIndex == asMoveChange.NewIndex && asMoveChange.NewIndex > asMoveChange.TabIndex)))
                                {
                                    var updatedMoveChange = (TabMovedDto) nextChange.UpdatedChange;
                                    updatedMoveChange.NewIndex--;
                                }   
                            }

                            var indexAfterApplyingNextChange =
                                mIndexCalculator.GetTabIndexAfterChanges(currentIndex, new[] {nextChange.OriginalChange});
                            if (indexAfterApplyingNextChange == null)
                            {
                                change.ShouldBeRemoved = true;
                                break;
                            }

                            currentIndex = indexAfterApplyingNextChange.Value;
                        }
                        break;
                    }

                    case TabUrlChangedDto tabUrlChanged:
                    {
                        var currentIndex = change.OriginalChange.TabIndex;

                        foreach (var nextChange in nextChanges)
                        {
                            if (currentIndex == nextChange.OriginalChange.TabIndex)
                            {
                                var asUrlChange = nextChange.OriginalChange as TabUrlChangedDto;
                                if (asUrlChange != null)
                                {
                                    tabUrlChanged.NewUrl = asUrlChange.NewUrl;
                                    nextChange.ShouldBeRemoved = true;
                                }
                            }

                            var indexAfterApplyingNextChange =
                                mIndexCalculator.GetTabIndexAfterChanges(currentIndex, new[] {nextChange.OriginalChange});
                            if (indexAfterApplyingNextChange == null)
                            {
                                break;
                            }

                            currentIndex = indexAfterApplyingNextChange.Value;
                        }
                        break;
                    }
                }

                if (!change.ShouldBeRemoved)
                {
                    yield return change.UpdatedChange;
                }
            }
        }

        private class ChangeWithState
        {
            public TabAction OriginalChange { get; set; }
            
            public TabAction UpdatedChange { get; set; }
            
            public bool ShouldBeRemoved { get; set; }
        }
    }
}