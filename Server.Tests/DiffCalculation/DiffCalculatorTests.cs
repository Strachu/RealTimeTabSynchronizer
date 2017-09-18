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
		public void ComputeChanges_ReturnsOnlyMoveActionWhenTabHasBeenMoved()
		{
			var serverTabs = new TabData[]
			{
				new TabData { Id = 1, Index = 2, Url = "1" },
				new TabData { Id = 2, Index = 3, Url = "2" },
				new TabData { Id = 3, Index = 1, Url = "3" },
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
			
			Assert.That(singleChange, Is.InstanceOf<TabMovedDto>());
			
			var tabClosedAction = (TabMovedDto)singleChange;
			
			Assert.That(tabClosedAction.TabIndex, Is.EqualTo(3));
			Assert.That(tabClosedAction.NewIndex, Is.EqualTo(1));
		}

		// TODO Url change detection
	}
}