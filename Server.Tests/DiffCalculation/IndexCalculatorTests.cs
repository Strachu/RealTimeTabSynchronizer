using System.Linq;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.DiffCalculation;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server.Tests.DiffCalculation
{
	[TestFixture]
	public class IndexCalculatorTests
	{
		private IIndexCalculator mIndexCalculator;

		[SetUp]
		public void SetUp()
		{
			mIndexCalculator = new IndexCalculator();
		}

		[Test]
		public void GetTabIndexBeforeChanges_ReturnsIndexIncrementedByOneIfTabBeforeThisOneHasBeenClosed()
		{
			var newIndex = 3;
			var changes = new TabAction[]
			{
				new TabClosedDto { TabIndex = 1 }
			};
			var previousIndex = mIndexCalculator.GetTabIndexBeforeChanges(newIndex, changes);

			Assert.That(previousIndex, Is.EqualTo(4));
		}
	}
}