using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.DiffCalculation;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server.Tests.DiffCalculation
{
	[TestFixture]
	public class ChangeListOptimizerTests
	{
		private IChangeListOptimizer mChangeListOptimizer;

		[SetUp]
		public void SetUp()
		{
			mChangeListOptimizer = new ChangeListOptimizer(new IndexCalculator());
		}

		[Test]
		public void WhenAllChangesAreDoneToDifferentTabs_TheListContainsAllActions()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 1, Url =  "http://www.google.com" },
				new TabMovedDto() { TabIndex = 0, NewIndex = 3 },
				new TabClosedDto { TabIndex = 4 },
				new TabUrlChangedDto() { TabIndex = 2, NewUrl = "new url" },
				new TabUrlChangedDto() { TabIndex = 5, NewUrl = "new url 2" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes);

			optimizedChanges.ShouldBeEquivalentTo(changes, opt => opt.RespectingRuntimeTypes());
		}

		[Test]
		public void WhenTabIsCreatedAndTheItsUrlChanges_JustCreateTabWithDestinationUrlIsReturned()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 1, Url =  "original url" },
				new TabCreatedDto() { TabIndex = 2, Url =  "noise" },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url" },
				new TabUrlChangedDto() { TabIndex = 3, NewUrl = "noise 2" },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url 2" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 1, Url = "new url 2" },
				new TabCreatedDto() { TabIndex = 2, Url = "noise" },
				new TabUrlChangedDto() { TabIndex = 3, NewUrl = "noise 2" },
			},
			opt => opt.RespectingRuntimeTypes());
		}

		[Test]
		public void MultipleUrlChangesOfTheSameTabAreMergedIntoOne()
		{
			var changes = new TabAction[]
			{
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url" },
				new TabUrlChangedDto() { TabIndex = 3, NewUrl = "noise" },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url 2" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabUrlChangedDto() {TabIndex = 1, NewUrl = "new url 2"},
				new TabUrlChangedDto() {TabIndex = 3, NewUrl = "noise"},
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenTabIsCreatedAndThenRemovedInTheSameSessionAllItsChangesAreRemoved()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 1, Url = "1" },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url" },
				new TabMovedDto() { TabIndex = 1, NewIndex = 3 },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "some other tab" },
				new TabUrlChangedDto() { TabIndex = 3, NewUrl = "new url 2" },
				new TabClosedDto() { TabIndex = 3 },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new [] { changes[3] }, opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void ChangesShouldNotBeRemovedWhenTabHasBeenRemovedNowButHasExistedBefore()
		{
			var changes = new TabAction[]
			{
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url" },
				new TabClosedDto() { TabIndex = 1 },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(changes, opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void ChangesOfIrrelevantTabsAreNotMergedEvenIfTheyShareIndices()
		{
			var changes = new TabAction[]
			{
				new TabClosedDto() { TabIndex = 1 },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "noise" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(changes, opt => opt.RespectingRuntimeTypes());
		}

		[Test]
		public void TabCreatedActionAtTheSameIndexAsPreviousTabIsHandledCorrectly()
		{
			var changes = new TabAction[]
			{
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "do not change this" },
				new TabCreatedDto() { TabIndex = 1, Url = "should be changed" },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "do not change this" },
				new TabCreatedDto() { TabIndex = 1, Url = "new url" },
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void OptimizerTakesIntoAccountChangesModyingIndicesOfAllTabsWhenDetectingTabByIndices()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto { TabIndex = 3, Url = "the url to change" },
				new TabCreatedDto { TabIndex = 2, Url = "url of second tab" },
				new TabUrlChangedDto() { TabIndex = 4, NewUrl = "new url" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto { TabIndex = 3, Url = "new url" },
				new TabCreatedDto { TabIndex = 2, Url = "url of second tab" },
			},
			opt => opt.RespectingRuntimeTypes());
		}

		[Test]
		public void ChangeGroupingShouldBeInterruptedByTabClosed()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 1, Url = "1" },
				new TabClosedDto() { TabIndex = 1 },
				new TabCreatedDto() { TabIndex = 1, Url = "2" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new [] { changes[2] }, opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_IndicesOfTabsCreatedAtIndexHigherThanRemovedAreAdjusted()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabCreatedDto() { TabIndex = 4, Url = "tab index 4" },
				new TabClosedDto() { TabIndex = 3 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "tab index 4" }
			},
			opt => opt.RespectingRuntimeTypes());
		}
				
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_IndicesOfTabsCreatedBeforeRemovedTabHasBeenCreatedAreLeftIntact()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 4, Url = "index not adjusted" },
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabClosedDto() { TabIndex = 3 },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new [] { changes[0] }, opt => opt.RespectingRuntimeTypes());
		}
				
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_IndicesOfTabsCreatedAfterRemovedTabHasBeenRemovedAreLeftIntact()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabClosedDto() { TabIndex = 3 },
				new TabCreatedDto() { TabIndex = 4, Url = "index not adjusted" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new [] { changes[2] }, opt => opt.RespectingRuntimeTypes());
		}
				
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_IndicesOfTabsCreatedAtIndexLowerAndEqualThanRemovedAreLeftIntact()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 2, Url = "tab index 2" },
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabCreatedDto() { TabIndex = 1, Url = "tab index 1" },
				new TabClosedDto() { TabIndex = 4 },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new [] { changes[0], changes[2] }, opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_IndicesOfTabsCreatedAtIndexHigherThanRemovedAreAdjusted_TakingIntoAccountMoveChanges()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabCreatedDto() { TabIndex = 4, Url = "tab index 4" },
				new TabMovedDto() { TabIndex = 3, NewIndex = 0 },
				new TabCreatedDto() { TabIndex = 1, Url = "tab index 1" },
				new TabClosedDto() { TabIndex = 0 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "tab index 4" },
				new TabCreatedDto() { TabIndex = 0, Url = "tab index 1" }
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_WhenTabIsCreatedAfterRemovedTabHasBeenMovedFurther_IndexOfThisTabIsLeftIntact()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabCreatedDto() { TabIndex = 4, Url = "noise" },
				new TabMovedDto() { TabIndex = 3, NewIndex = 5 },
				new TabCreatedDto() { TabIndex = 4, Url = "tab left intact" },
				new TabClosedDto() { TabIndex = 6 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "noise" },
				new TabCreatedDto() { TabIndex = 4, Url = "tab left intact" },
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_ChangesToOtherTabsAreStillCorrectlyCorrelatedAfterItsIndexChanges()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabCreatedDto() { TabIndex = 4, Url = "tab index 4" },
				new TabUrlChangedDto() { TabIndex = 4, NewUrl = "tab index 4 new url" },
				new TabMovedDto() { TabIndex = 3, NewIndex = 5 },
				new TabUrlChangedDto() { TabIndex = 3, NewUrl = "tab index 4 new url 2" },
				new TabClosedDto() { TabIndex = 5 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "tab index 4 new url 2" },
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_IndicesOfActionsOtherThanCreateTabAreAlsoUpdated()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabCreatedDto() { TabIndex = 4, Url = "tab index 4" },
				new TabUrlChangedDto() { TabIndex = 4, NewUrl = "tab index 4 new url" },
				new TabMovedDto() { TabIndex = 3, NewIndex = 0 },
				new TabUrlChangedDto() { TabIndex = 2, NewUrl = "new url for index 2" },
				new TabMovedDto() { TabIndex = 5, NewIndex = 3 },
				new TabClosedDto() { TabIndex = 0 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "tab index 4 new url" },
				new TabUrlChangedDto() { TabIndex = 1, NewUrl = "new url for index 2" },
				new TabMovedDto() { TabIndex = 4, NewIndex = 2 },
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_WhenTabIsMovedFromBeforeToAfterRemovedTab_OnlyNewIndexOfMoveActionIsAdjusted()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabMovedDto() { TabIndex = 1, NewIndex = 5 },
				new TabClosedDto() { TabIndex = 2 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabMovedDto() { TabIndex = 1, NewIndex = 4 },
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_WhenTabIsMovedFromBeforeToAfterRemovedTab_AtTheSameIndexAsRemoved_ItsNewIndexIsAdjusted()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabMovedDto() { TabIndex = 1, NewIndex = 3 },
				new TabClosedDto() { TabIndex = 2 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabMovedDto() { TabIndex = 1, NewIndex = 2 },
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_WhenTabIsMovedFromAfterToBeforeRemovedTab_AtTheSameIndexAsRemoved_ItsNewIndexIsNotAdjusted()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabMovedDto() { TabIndex = 5, NewIndex = 3 },
				new TabClosedDto() { TabIndex = 4 }
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabMovedDto() { TabIndex = 4, NewIndex = 3 },
			},
			opt => opt.RespectingRuntimeTypes());
		}
		
		[Test]
		public void WhenAllChangesRelatedToTabCreatedAndRemovedInSingleSessionAreDiscarded_ForMultipleTabs_IndicesOfTabsCreatedAfterAllRemovedTabsAreReducedByTheNumberOfThoseTabs()
		{
			var changes = new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "to be removed" },
				new TabCreatedDto() { TabIndex = 4, Url = "tab index 4 adjusted to 3" },
				new TabCreatedDto() { TabIndex = 6, Url = "to be removed 2" },
				new TabUrlChangedDto() { TabIndex = 7, NewUrl = "new url for index 7 adjusted to 5" },
				new TabUrlChangedDto() { TabIndex = 4, NewUrl = "tab index 4 adjusted to 3 new url" },
				new TabClosedDto() { TabIndex = 3 },
				new TabUrlChangedDto() { TabIndex = 4, NewUrl = "new url for index 4 not adjusted" },
				new TabUrlChangedDto() { TabIndex = 8, NewUrl = "new url for index 8 adjusted to 7" },
				new TabUrlChangedDto() { TabIndex = 3, NewUrl = "tab index 4 adjusted to 3 new url 2" },
				new TabClosedDto() { TabIndex = 5 },
				new TabUrlChangedDto() { TabIndex = 9, NewUrl = "new url for index 9 not adjusted" },
			};
			
			var optimizedChanges = mChangeListOptimizer.GetOptimizedList(changes).ToList();

			optimizedChanges.ShouldBeEquivalentTo(new TabAction[]
			{
				new TabCreatedDto() { TabIndex = 3, Url = "tab index 4 adjusted to 3 new url 2" },
				new TabUrlChangedDto() { TabIndex = 5, NewUrl = "new url for index 7 adjusted to 5" },
				new TabUrlChangedDto() { TabIndex = 4, NewUrl = "new url for index 4 not adjusted" },
				new TabUrlChangedDto() { TabIndex = 7, NewUrl = "new url for index 8 adjusted to 7" },
				new TabUrlChangedDto() { TabIndex = 9, NewUrl = "new url for index 9 not adjusted" },
			},
			opt => opt.RespectingRuntimeTypes());
		}
	}
}