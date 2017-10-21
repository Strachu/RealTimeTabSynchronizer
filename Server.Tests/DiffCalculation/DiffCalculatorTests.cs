using System;
using System.Linq;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.DiffCalculation;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server.Tests.DiffCalculation
{
	[TestFixture]
	public class DiffCalculatorTests
	{
		private IDiffCalculator mDiffCalculator;

		[SetUp]
		public void SetUp()
		{
			mDiffCalculator = new DiffCalculator();
		}

		[Test]
		public void ComputeChanges_ReturnsNothingWhenNothingChanged()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
				new TabData { Id = 3, Index = 3, Url = "3" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs);
			
			Assert.That(computedChanges, Is.Empty);
		}

		[Test]
		public void ComputeChanges_ReturnsOnlyAddActionWhenTabHasBeenAddedAtTheEnd()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
				new TabData { Id = 3, Index = 3, Url = "3" },
				new TabData { Id = 4, Index = 4, Url = "4" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabCreatedDto>());
			
			var tabCreatedAction = (TabCreatedDto)singleChange;
			
			Assert.That(tabCreatedAction.TabIndex, Is.EqualTo(4));
			Assert.That(tabCreatedAction.Url, Is.EqualTo("4"));
		}

		[Test]
		public void ComputeChanges_ReturnsOnlyAddActionWhenTabHasBeenAddedInTheMiddle()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 4, Index = 2, Url = "4" },
				new TabData { Id = 2, Index = 3, Url = "2" },
				new TabData { Id = 3, Index = 4, Url = "3" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[2]),
				NewBrowserTab(index: 3, serverTab: serverTabs[3]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabCreatedDto>());
			
			var tabCreatedAction = (TabCreatedDto)singleChange;
			
			Assert.That(tabCreatedAction.TabIndex, Is.EqualTo(2));
			Assert.That(tabCreatedAction.Url, Is.EqualTo("4"));
		}

		[Test]
		public void ComputeChanges_DoesNotReturnAddActionForTabsWhichHaveBeenCreatedAndTheClosed()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
				new TabData { Id = 3, Index = 3, Url = "3" },
				new TabData { Id = 4, Index = null, Url = "4" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();

			Assert.That(computedChanges, Is.Empty);
		}

		[Test]
		public void ComputeChanges_ReturnsAddActionsWithCorrectIndicesWhen2TabsHasBeenAdded()
		{
			// 12 -> 132 -> 1432
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 4, Url = "2" },
				new TabData { Id = 3, Index = 3, Url = "3" },
				new TabData { Id = 4, Index = 2, Url = "4" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var addActions = computedChanges.Cast<TabCreatedDto>().ToList();
			
			// Just different order of operations than original
			Assert.That(addActions.Count, Is.EqualTo(2));
			Assert.That(addActions[0].TabIndex, Is.EqualTo(2));
			Assert.That(addActions[0].Url, Is.EqualTo("4"));
			Assert.That(addActions[1].TabIndex, Is.EqualTo(3));
			Assert.That(addActions[1].Url, Is.EqualTo("3"));
		}

		[Test]
		public void ComputeChanges_ReturnsOnlyCloseActionWhenTabHasBeenClosedAtTheEnd()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: new TabData { Index = null, Url = "3" }),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabClosedDto>());
			
			var tabClosedAction = (TabClosedDto)singleChange;
			
			Assert.That(tabClosedAction.TabIndex, Is.EqualTo(3));
		}

		[Test]
		public void ComputeChanges_ReturnsOnlyCloseActionWhenTabHasBeenClosedInTheMiddle()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: new TabData { Index = null, Url = "3" }),
				NewBrowserTab(index: 3, serverTab: serverTabs[1]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabClosedDto>());
			
			var tabClosedAction = (TabClosedDto)singleChange;
			
			Assert.That(tabClosedAction.TabIndex, Is.EqualTo(2));
		}

		[Test]
		public void ComputeChanges_ReturnsCloseActionsWithCorrectIndicesWhen2TabsHasBeenClosed()
		{
			// 1234 -> 124 -> 24
			var serverTabs = new TabData[]
			{
				new TabData { Id = 2, Index = 1, Url = "2" },
				new TabData { Id = 4, Index = 2, Url = "4" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: new TabData { Index = null, Url = "1" }),
				NewBrowserTab(index: 2, serverTab: serverTabs[0]),
				NewBrowserTab(index: 3, serverTab: new TabData { Index = null, Url = "3" }),
				NewBrowserTab(index: 4, serverTab: serverTabs[1]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var closeActions = computedChanges.Cast<TabClosedDto>().ToList();
			
			// Just different order of operations than original
			Assert.That(closeActions.Count, Is.EqualTo(2));
			Assert.That(closeActions[0].TabIndex, Is.EqualTo(1));
			Assert.That(closeActions[1].TabIndex, Is.EqualTo(2));
		}

		[Test]
		public void ComputeChanges_ReturnsOnlyMoveActionWhenTabHasBeenMovedForwardInSingleAction()
		{
			// 12345 -> 13452
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 5, Url = "2" },
				new TabData { Id = 3, Index = 2, Url = "3" },
				new TabData { Id = 4, Index = 3, Url = "4" },
				new TabData { Id = 5, Index = 4, Url = "5" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabMovedDto>());
			
			var tabClosedAction = (TabMovedDto)singleChange;
			
			Assert.That(tabClosedAction.TabIndex, Is.EqualTo(2));
			Assert.That(tabClosedAction.NewIndex, Is.EqualTo(5));
		}

		[Test]
		public void ComputeChanges_ReturnsOnlyMoveActionWhenTabHasBeenMovedBackwardsInSingleAction()
		{
			// 12345 -> 14235
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 3, Url = "2" },
				new TabData { Id = 3, Index = 4, Url = "3" },
				new TabData { Id = 4, Index = 2, Url = "4" },
				new TabData { Id = 5, Index = 5, Url = "5" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabMovedDto>());
			
			var tabClosedAction = (TabMovedDto)singleChange;
			
			Assert.That(tabClosedAction.TabIndex, Is.EqualTo(4));
			Assert.That(tabClosedAction.NewIndex, Is.EqualTo(2));
		}

		[Test]
		public void ComputeChanges_Returns2MoveActionWhen2TabsHasBeenMovedForward()
		{
			// 1234567 -> 1345627 -> 1456327
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 6, Url = "2" },
				new TabData { Id = 3, Index = 5, Url = "3" },
				new TabData { Id = 4, Index = 2, Url = "4" },
				new TabData { Id = 5, Index = 3, Url = "5" },
				new TabData { Id = 6, Index = 4, Url = "6" },
				new TabData { Id = 7, Index = 7, Url = "7" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
				NewBrowserTab(index: 6, serverTab: serverTabs[5]),
				NewBrowserTab(index: 7, serverTab: serverTabs[6]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var moveActions = computedChanges.Cast<TabMovedDto>().ToList();
			
			Assert.That(moveActions.Count, Is.EqualTo(2));
			Assert.That(moveActions[0].TabIndex, Is.EqualTo(2));
			Assert.That(moveActions[0].NewIndex, Is.EqualTo(6));
			Assert.That(moveActions[1].TabIndex, Is.EqualTo(2));
			Assert.That(moveActions[1].NewIndex, Is.EqualTo(5));
		}

		[Test]
		public void ComputeChanges_Returns2MoveActionWhen2TabsHasBeenMovedBackwards()
		{
			// 1234567 -> 1623457 -> 1652347
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 4, Url = "2" },
				new TabData { Id = 3, Index = 5, Url = "3" },
				new TabData { Id = 4, Index = 6, Url = "4" },
				new TabData { Id = 5, Index = 3, Url = "5" },
				new TabData { Id = 6, Index = 2, Url = "6" },
				new TabData { Id = 7, Index = 7, Url = "7" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
				NewBrowserTab(index: 6, serverTab: serverTabs[5]),
				NewBrowserTab(index: 7, serverTab: serverTabs[6]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var moveActions = computedChanges.Cast<TabMovedDto>().ToList();
			
			Assert.That(moveActions.Count, Is.EqualTo(2));
			Assert.That(moveActions[0].TabIndex, Is.EqualTo(6));
			Assert.That(moveActions[0].NewIndex, Is.EqualTo(2));
			Assert.That(moveActions[1].TabIndex, Is.EqualTo(6));
			Assert.That(moveActions[1].NewIndex, Is.EqualTo(3));
		}
		
		[Test]
		public void ComputeChanges_Returns2MoveActionWhen2TabsHasBeenMovedBackwardsInReversedOrder()
		{
			// 1234567 -> 1253467 -> 1625347
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 3, Url = "2" },
				new TabData { Id = 3, Index = 5, Url = "3" },
				new TabData { Id = 4, Index = 6, Url = "4" },
				new TabData { Id = 5, Index = 4, Url = "5" },
				new TabData { Id = 6, Index = 2, Url = "6" },
				new TabData { Id = 7, Index = 7, Url = "7" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
				NewBrowserTab(index: 6, serverTab: serverTabs[5]),
				NewBrowserTab(index: 7, serverTab: serverTabs[6]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var moveActions = computedChanges.Cast<TabMovedDto>().ToList();
			
			// The moves returned by the algorithm are a bit different than original 
			// but the result is the same so this is just fine.
			Assert.That(moveActions.Count, Is.EqualTo(2));
			Assert.That(moveActions[0].TabIndex, Is.EqualTo(6));
			Assert.That(moveActions[0].NewIndex, Is.EqualTo(2));
			Assert.That(moveActions[1].TabIndex, Is.EqualTo(6));
			Assert.That(moveActions[1].NewIndex, Is.EqualTo(4));
		}
		
		[Test]
		public void ComputeChanges_Returns2MoveActionWhen2TabsHasBeenMovedFirstBackwardThenForward()
		{
			// 123456789 -> 12735689 -> 127456389
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
				new TabData { Id = 3, Index = 7, Url = "3" },
				new TabData { Id = 4, Index = 4, Url = "4" },
				new TabData { Id = 5, Index = 5, Url = "5" },
				new TabData { Id = 6, Index = 6, Url = "6" },
				new TabData { Id = 7, Index = 3, Url = "7" },
				new TabData { Id = 8, Index = 8, Url = "8" },
				new TabData { Id = 9, Index = 9, Url = "9" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
				NewBrowserTab(index: 6, serverTab: serverTabs[5]),
				NewBrowserTab(index: 7, serverTab: serverTabs[6]),
				NewBrowserTab(index: 8, serverTab: serverTabs[7]),
				NewBrowserTab(index: 9, serverTab: serverTabs[8]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var moveActions = computedChanges.Cast<TabMovedDto>().ToList();
			
			// Different than original but the result is the same
			Assert.That(moveActions.Count, Is.EqualTo(2));
			Assert.That(moveActions[0].TabIndex, Is.EqualTo(3));
			Assert.That(moveActions[0].NewIndex, Is.EqualTo(7));
			Assert.That(moveActions[1].TabIndex, Is.EqualTo(6));
			Assert.That(moveActions[1].NewIndex, Is.EqualTo(3));
		}
		
		[Test]
		public void ComputeChanges_ReturnsCorrectMoveActionWhen2TabsHasBeenMovedFirstForwardThenBackwards()
		{
			// 123456789 -> 13562789 -> 134856279
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 7, Url = "2" },
				new TabData { Id = 3, Index = 2, Url = "3" },
				new TabData { Id = 4, Index = 3, Url = "4" },
				new TabData { Id = 5, Index = 5, Url = "5" },
				new TabData { Id = 6, Index = 6, Url = "6" },
				new TabData { Id = 7, Index = 8, Url = "7" },
				new TabData { Id = 8, Index = 4, Url = "8" },
				new TabData { Id = 9, Index = 9, Url = "9" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
				NewBrowserTab(index: 6, serverTab: serverTabs[5]),
				NewBrowserTab(index: 7, serverTab: serverTabs[6]),
				NewBrowserTab(index: 8, serverTab: serverTabs[7]),
				NewBrowserTab(index: 9, serverTab: serverTabs[8]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var moveActions = computedChanges.Cast<TabMovedDto>().ToList();
			
			// The algorithm returned 3 actions while the original has been derived in 2 steps.
			// The result is the same nonetheless but it shows that the algorithm could be optimized.
			Assert.That(moveActions.Count, Is.EqualTo(3));
			Assert.That(moveActions[0].TabIndex, Is.EqualTo(2));
			Assert.That(moveActions[0].NewIndex, Is.EqualTo(7));
			Assert.That(moveActions[1].TabIndex, Is.EqualTo(8));
			Assert.That(moveActions[1].NewIndex, Is.EqualTo(4));
			Assert.That(moveActions[2].TabIndex, Is.EqualTo(8));
			Assert.That(moveActions[2].NewIndex, Is.EqualTo(7));
		}
		
		[Test]
		public void ComputeChanges_ReturnsCorrectMoveActionsWhen3MovesHasBeenMade2For1Tab()
		{
			// 123456789 -> 123645789 -> 126457389 -> 124573869
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
				new TabData { Id = 3, Index = 6, Url = "3" },
				new TabData { Id = 4, Index = 3, Url = "4" },
				new TabData { Id = 5, Index = 4, Url = "5" },
				new TabData { Id = 6, Index = 8, Url = "6" },
				new TabData { Id = 7, Index = 5, Url = "7" },
				new TabData { Id = 8, Index = 7, Url = "8" },
				new TabData { Id = 9, Index = 9, Url = "9" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, serverTab: serverTabs[3]),
				NewBrowserTab(index: 5, serverTab: serverTabs[4]),
				NewBrowserTab(index: 6, serverTab: serverTabs[5]),
				NewBrowserTab(index: 7, serverTab: serverTabs[6]),
				NewBrowserTab(index: 8, serverTab: serverTabs[7]),
				NewBrowserTab(index: 9, serverTab: serverTabs[8]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			var moveActions = computedChanges.Cast<TabMovedDto>().ToList();
			
			// Different than original but the result is the same
			Assert.That(moveActions.Count, Is.EqualTo(3));
			Assert.That(moveActions[0].TabIndex, Is.EqualTo(3));
			Assert.That(moveActions[0].NewIndex, Is.EqualTo(6));
			Assert.That(moveActions[1].TabIndex, Is.EqualTo(5));
			Assert.That(moveActions[1].NewIndex, Is.EqualTo(8));
			Assert.That(moveActions[2].TabIndex, Is.EqualTo(5));
			Assert.That(moveActions[2].NewIndex, Is.EqualTo(6));
		}
		
		[Test]
		public void ComputeChanges_ReturnsCorrectChangesWhenAllActionTypesChangingIndicesAreExecutedAtOnce()
		{
			// 12345 -> 1235 -> 16235 -> 62135 -> 6215
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 3, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2" },
				new TabData { Id = 5, Index = 4, Url = "5" },
				new TabData { Id = 6, Index = 1, Url = "6" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, serverTab: new TabData { Index = null, Url = "3" }),
				NewBrowserTab(index: 4, serverTab: new TabData { Index = null, Url = "4" }),
				NewBrowserTab(index: 5, serverTab: serverTabs[2]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			// A little different than original but the result is the same with the same amount of steps
			Assert.That(computedChanges.Count, Is.EqualTo(4));
			Assert.That(computedChanges[0], Is.InstanceOf<TabCreatedDto>());
			var action1 = (TabCreatedDto)computedChanges[0];
			
			Assert.That(action1.TabIndex, Is.EqualTo(1));
			Assert.That(action1.Url, Is.EqualTo("6"));
			
			Assert.That(computedChanges[1], Is.InstanceOf<TabClosedDto>());
			var action2 = (TabClosedDto)computedChanges[1];
			
			Assert.That(action2.TabIndex, Is.EqualTo(4));
			
			Assert.That(computedChanges[2], Is.InstanceOf<TabClosedDto>());
			var action3 = (TabClosedDto)computedChanges[2];
			
			Assert.That(action3.TabIndex, Is.EqualTo(4));
			
			Assert.That(computedChanges[3], Is.InstanceOf<TabMovedDto>());
			var action4 = (TabMovedDto)computedChanges[3];
			
			Assert.That(action4.TabIndex, Is.EqualTo(2));
			Assert.That(action4.NewIndex, Is.EqualTo(3));
		}
		
		[Test]
		public void ComputeChanges_ReturnsUrlChangeWhenOriginalBrowserUrlDiffersFromServerTabUrl()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 1, Url = "1" },
				new TabData { Id = 2, Index = 2, Url = "2 changed" },
				new TabData { Id = 3, Index = 3, Url = "3" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, browserUrl: "1", serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, browserUrl: "2", serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, browserUrl: "3", serverTab: serverTabs[2]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabUrlChangedDto>());
			
			var urlChangedAction = (TabUrlChangedDto)singleChange;
			
			Assert.That(urlChangedAction.TabIndex, Is.EqualTo(2));
			Assert.That(urlChangedAction.NewUrl, Is.EqualTo("2 changed"));
		}
		
		[Test]
		public void ComputeChanges_ReturnsCorrectUrlChangeWhenOriginalBrowserUrlDiffersFromServerTabUrlAndHasBeenMoved()
		{
			// 1234 -> 1324 -> 4132
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 2, Url = "1" },
				new TabData { Id = 2, Index = 4, Url = "2 changed" },
				new TabData { Id = 3, Index = 3, Url = "3" },
				new TabData { Id = 4, Index = 1, Url = "4" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 1, browserUrl: "1", serverTab: serverTabs[0]),
				NewBrowserTab(index: 2, browserUrl: "2", serverTab: serverTabs[1]),
				NewBrowserTab(index: 3, browserUrl: "3", serverTab: serverTabs[2]),
				NewBrowserTab(index: 4, browserUrl: "4", serverTab: serverTabs[3]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(3));
			Assert.That(computedChanges[0], Is.InstanceOf<TabMovedDto>());
			Assert.That(computedChanges[1], Is.InstanceOf<TabMovedDto>());
			Assert.That(computedChanges[2], Is.InstanceOf<TabUrlChangedDto>());
			var urlChangedAction = (TabUrlChangedDto)computedChanges[2];
			
			Assert.That(urlChangedAction.TabIndex, Is.EqualTo(4));
			Assert.That(urlChangedAction.NewUrl, Is.EqualTo("2 changed"));
		}
		
		[Test]
		public void ComputeChanges_DoesNotReturnUrlChangeForRemovedTabs()
		{
			// 012 -> 1(url) -> 02 -> 0
			var serverTabs = new TabData[]
			{
				new TabData { Id = 0, Index = 0, Url = "0" },
				new TabData { Id = 1, Index = null, Url = "1 changed" },
				new TabData { Id = 2, Index = null, Url = "2" },
			};

			var browserTabs = new BrowserTab[]
			{
				NewBrowserTab(index: 0, browserUrl: "0", serverTab: serverTabs[0]),
				NewBrowserTab(index: 1, browserUrl: "1", serverTab: serverTabs[1]),
				NewBrowserTab(index: 2, browserUrl: "2", serverTab: serverTabs[2]),
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.OfType<TabUrlChangedDto>().Count, Is.EqualTo(0));
		}

		private BrowserTab NewBrowserTab(int index, TabData serverTab, string browserUrl = null)
		{
			return new BrowserTab
			{
				Index = index,
				ServerTab = serverTab,
				Url = browserUrl ?? serverTab.Url
			};
		}
	}
}