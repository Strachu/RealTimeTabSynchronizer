using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Tests
{
	[TestFixture]
	public class TabServiceTests
	{
		private TabService mTabService;

		private Mock<ITabDataRepository> mTabDataRepositoryMock;
		private Mock<IBrowserTabRepository> mBrowserTabRepositoryMock;
		private Mock<IActiveTabDao> mActiveTabDaoMock;
		private Mock<TabSynchronizerDbContext> mDbContextMock;
		private Mock<ILogger<TabService>> mLoggerMock;

		[SetUp]
		public void SetUp()
		{
			mTabDataRepositoryMock = new Mock<ITabDataRepository>();
			mBrowserTabRepositoryMock = new Mock<IBrowserTabRepository>();
			mActiveTabDaoMock = new Mock<IActiveTabDao>();
			mDbContextMock = new Mock<TabSynchronizerDbContext>(new DbContextOptions<TabSynchronizerDbContext>(), new Mock<IModelBuildingService>().Object);
			mLoggerMock = new Mock<ILogger<TabService>>();

			mTabService = new TabService(
				mDbContextMock.Object,
				mLoggerMock.Object,
				mTabDataRepositoryMock.Object,
				mBrowserTabRepositoryMock.Object,
				mActiveTabDaoMock.Object);
		}

		[Test]
		public async Task AddTab_IncrementsTheIndicesOfBrowserTabsLyingAfterAddedTab()
		{
			var browserId = Guid.NewGuid();
			var newTabIndex = 3;

			await mTabService.AddTab(browserId, tabId: 1, tabIndex: newTabIndex, url: "any", createInBackground: true);

			mBrowserTabRepositoryMock.Verify(x => x.IncrementTabIndices(
				browserId,
				It.Is<TabRange>(y => y.FromIndexInclusive == 3),
				1));
		}

		[Test]
		public async Task MoveTab_AlsoBrowserTabIndexIsUpdated()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var oldTabIndex = 5;
			var newTabIndex = 3;

			var browserTab = new BrowserTab()
			{
				Index = oldTabIndex,
				ServerTab = new TabData()
				{
					Index = oldTabIndex
				}
			};
			
			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(browserTab));

			await mTabService.MoveTab(browserId, tabId, newTabIndex);

			Assert.That(browserTab.Index, Is.EqualTo(newTabIndex));
		}

		[Test]
		public async Task MoveTab_WhenTabHasBeenMovedBeforeItsPreviousIndex_IncrementsTheIndicesOfBrowserTabsLyingAfterNewIndexButBeforeOldIndex()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var oldTabIndex = 5;
			var newTabIndex = 3;

			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(new BrowserTab()
			{
				Index = oldTabIndex,
				ServerTab = new TabData()
				{
					Index = oldTabIndex
				}
			}));

			await mTabService.MoveTab(browserId, tabId, newTabIndex);

			mBrowserTabRepositoryMock.Verify(x => x.IncrementTabIndices(
				browserId,
				It.Is<TabRange>(y => y.FromIndexInclusive == 3 && y.ToIndexInclusive == 4),
				1));
		}

		[Test]
		public async Task MoveTab_WhenTabHasBeenMovedAfterItsPreviousIndex_DecrementsTheIndicesOfBrowserTabsLyingBeforeNewIndexButAfterOldIndex()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var oldTabIndex = 3;
			var newTabIndex = 5;

			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(new BrowserTab()
			{
				Index = oldTabIndex,
				ServerTab = new TabData()
				{
					Index = oldTabIndex
				}
			}));

			await mTabService.MoveTab(browserId, tabId, newTabIndex);

			mBrowserTabRepositoryMock.Verify(x => x.IncrementTabIndices(
				browserId,
				It.Is<TabRange>(y => y.FromIndexInclusive == 4 && y.ToIndexInclusive == 5),
				-1));
		}
	
		/// <summary>
		/// It happens when 2 browsers are online, when tab is moved on first one, server tab is updated,
		/// then an event comes to second browser resulting in second moveTabs call, this time the server
		/// tab is in correct position, only the browser tabs indices needs updating
		/// </summary>	
		[Test]
		public async Task MoveTab_WhenServerTabIsAlreadyOnSpecifiedPosition_BrowserTabIndexIsStillUpdated()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var oldTabIndex = 5;
			var newTabIndex = 3;

			var browserTab = new BrowserTab()
			{
				Index = oldTabIndex,
				ServerTab = new TabData()
				{
					Index = newTabIndex
				}
			};
			
			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(browserTab));

			await mTabService.MoveTab(browserId, tabId, newTabIndex);

			Assert.That(browserTab.Index, Is.EqualTo(newTabIndex));
		}

		/// <summary>
		/// It happens when 2 browsers are online, when tab is moved on first one, server tab is updated,
		/// then an event comes to second browser resulting in second moveTabs call, this time the server
		/// tab is in correct position, only the browser tabs indices needs updating
		/// </summary>
		[Test]
		public async Task MoveTab_WhenServerTabIsAlreadyOnSpecifiedPosition_TheIndicesOfBrowserTabsAreStillUpdated()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var oldTabIndex = 5;
			var newTabIndex = 3;

			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(new BrowserTab()
			{
				Index = oldTabIndex,
				ServerTab = new TabData()
				{
					Index = newTabIndex
				}
			}));

			await mTabService.MoveTab(browserId, tabId, newTabIndex);

			mBrowserTabRepositoryMock.Verify(x => x.IncrementTabIndices(browserId, It.IsAny<TabRange>(), It.IsAny<int>()));
		}
	
		/// <summary>
		/// It may possibly happen when some old events are queued on 1 browser.
		/// </summary>
		[Test]
		public async Task MoveTab_WhenServerTabHasAlreadyBeenRemoved_TheIndicesOfBrowserTabsAreStillUpdated()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var oldTabIndex = 5;
			var newTabIndex = 3;

			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(new BrowserTab()
			{
				Index = oldTabIndex,
				ServerTab = new TabData()
				{
					IsOpen = false
				}
			}));

			await mTabService.MoveTab(browserId, tabId, newTabIndex);

			mBrowserTabRepositoryMock.Verify(x => x.IncrementTabIndices(browserId, It.IsAny<TabRange>(), It.IsAny<int>()));
		}
	
		/// <summary>
		/// It happens when 2 browsers are online, when tab is closed on first one, server tab is updated,
		/// then an event comes to second browser resulting in second CloseTab call, this time the server
		/// tab is already removed, only the browser tabs needs to be removed.
		/// </summary>
		[Test]
		public void CloseTab_WhenServerTabHasAlreadyBeenClosed_NoExceptionOccursAndTheIndicesOfServerTabsAreNotUpdated()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var tabIndex = 5;

			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(new BrowserTab()
			{
				Index = tabIndex,
				ServerTab = new TabData()
				{
					IsOpen = false
				}
			}));

			Assert.DoesNotThrowAsync(async () => await mTabService.CloseTab(browserId, tabId));

			mTabDataRepositoryMock.Verify(x => x.IncrementTabIndices(It.IsAny<TabRange>(), It.IsAny<int>()), Times.Never);
		}
		
		[Test]
		public async Task CloseTab_RemovesBrowserTab()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var tabIndex = 5;
			var browserTab = new BrowserTab()
			{
				Index = tabIndex,
				ServerTab = new TabData()
				{
					Index = tabIndex
				}
			};
			
			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(browserTab));

			await mTabService.CloseTab(browserId, tabId);

			mBrowserTabRepositoryMock.Verify(x => x.Remove(browserTab));
		}
		
		[Test]
		public async Task CloseTab_DecrementsTheIndicesOfBrowserTabsLyingAfterClosedTab()
		{
			var browserId = Guid.NewGuid();
			var tabId = 1;
			var tabIndex = 5;
			var browserTab = new BrowserTab()
			{
				Index = tabIndex,
				ServerTab = new TabData()
				{
					Index = tabIndex
				}
			};
			
			mBrowserTabRepositoryMock.Setup(x => x.GetByBrowserTabId(browserId, tabId)).Returns(Task.FromResult(browserTab));

			await mTabService.CloseTab(browserId, tabId);

			mBrowserTabRepositoryMock.Verify(x => x.IncrementTabIndices(
				browserId,
				It.Is<TabRange>(y => y.FromIndexInclusive == 6),
				-1));
		}
	}
}