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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[3] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();

			Assert.That(computedChanges, Is.Empty);
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = new TabData { Index = null, Url = "3" } },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = new TabData { Index = null, Url = "3" } },
				new BrowserTab { Index = 3, ServerTab = serverTabs[1] },
			};

			var computedChanges = mDiffCalculator.ComputeChanges(browserTabs, serverTabs).ToList();
			
			Assert.That(computedChanges.Count, Is.EqualTo(1));

			var singleChange = computedChanges.Single();
			
			Assert.That(singleChange, Is.InstanceOf<TabClosedDto>());
			
			var tabClosedAction = (TabClosedDto)singleChange;
			
			Assert.That(tabClosedAction.TabIndex, Is.EqualTo(2));
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
				new BrowserTab { Index = 6, ServerTab = serverTabs[5] },
				new BrowserTab { Index = 7, ServerTab = serverTabs[6] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
				new BrowserTab { Index = 6, ServerTab = serverTabs[5] },
				new BrowserTab { Index = 7, ServerTab = serverTabs[6] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
				new BrowserTab { Index = 6, ServerTab = serverTabs[5] },
				new BrowserTab { Index = 7, ServerTab = serverTabs[6] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
				new BrowserTab { Index = 6, ServerTab = serverTabs[5] },
				new BrowserTab { Index = 7, ServerTab = serverTabs[6] },
				new BrowserTab { Index = 8, ServerTab = serverTabs[7] },
				new BrowserTab { Index = 9, ServerTab = serverTabs[8] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
				new BrowserTab { Index = 6, ServerTab = serverTabs[5] },
				new BrowserTab { Index = 7, ServerTab = serverTabs[6] },
				new BrowserTab { Index = 8, ServerTab = serverTabs[7] },
				new BrowserTab { Index = 9, ServerTab = serverTabs[8] },
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
				new BrowserTab { Index = 1, ServerTab = serverTabs[0] },
				new BrowserTab { Index = 2, ServerTab = serverTabs[1] },
				new BrowserTab { Index = 3, ServerTab = serverTabs[2] },
				new BrowserTab { Index = 4, ServerTab = serverTabs[3] },
				new BrowserTab { Index = 5, ServerTab = serverTabs[4] },
				new BrowserTab { Index = 6, ServerTab = serverTabs[5] },
				new BrowserTab { Index = 7, ServerTab = serverTabs[6] },
				new BrowserTab { Index = 8, ServerTab = serverTabs[7] },
				new BrowserTab { Index = 9, ServerTab = serverTabs[8] },
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
		
		// TODO Multiple action combined
		
		// TODO Url change detection
	}
}