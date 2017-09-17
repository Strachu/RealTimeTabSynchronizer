using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tests.TestTools;
using RealTimeTabSynchronizer.Server.Browsers;
using System;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RealTimeTabSynchronizer.Server.Tests.IntegrationTests
{
	[TestFixtureForAllDatabases]
	public class BrowserTabRepositoryTests
	{
		private readonly DbContextFactory mCurrentDatabaseContextFactory;

		private IBrowserTabRepository mBrowserTabRepository;
		private TabSynchronizerDbContext mDbContext;
		private IDbContextTransaction mTransaction;

		private Guid mBrowserId;
		private int mServerTabId;

		public BrowserTabRepositoryTests(DbContextFactory contextFactory)
		{
			mCurrentDatabaseContextFactory = contextFactory;
		}

		[SetUp]
		public void SetUp()
		{
			mDbContext = mCurrentDatabaseContextFactory.Create();
			mTransaction = mDbContext.Database.BeginTransaction();

			mBrowserTabRepository = new BrowserTabRepository(mDbContext);

			mBrowserId = Guid.NewGuid();
			mDbContext.Browsers.Add(new Browser { Id = mBrowserId, Name = "BrowserName" });
			var tabData = new TabData { Url = "http://www.google.com" };
			mDbContext.Tabs.Add(tabData);
			mDbContext.SaveChanges();

			mServerTabId = tabData.Id;
		}

		[TearDown]
		public void TearDown()
		{
			mTransaction.Dispose();
			mDbContext.Dispose();
		}

		[Test]
		public async Task IncrementTabIndices_IncrementsTheIndicesOfAllTabsInSpecifiedRangeByPositiveAmount()
		{
			AddBrowserTab(id: 1, index: 1);
			AddBrowserTab(id: 2, index: 2);
			AddBrowserTab(id: 3, index: 3);
			AddBrowserTab(id: 5, index: 5);
			mDbContext.SaveChanges();

			await mBrowserTabRepository.IncrementTabIndices(mBrowserId, new TabRange(2, 3), incrementBy: 1);

			var tabs = mDbContext.BrowserTabs.AsNoTracking().ToList();

			Assert.That(tabs.Single(x => x.BrowserTabId == 1).Index, Is.EqualTo(1));
			Assert.That(tabs.Single(x => x.BrowserTabId == 2).Index, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.BrowserTabId == 3).Index, Is.EqualTo(4));
			Assert.That(tabs.Single(x => x.BrowserTabId == 5).Index, Is.EqualTo(5));
		}

		[Test]
		public async Task IncrementTabIndices_DecrementsTheIndicesOfAllTabsInSpecifiedRangeWhenNegativeAmountHasBeenSpecified()
		{
			AddBrowserTab(id: 1, index: 1);
			AddBrowserTab(id: 2, index: 2);
			AddBrowserTab(id: 4, index: 4);
			AddBrowserTab(id: 5, index: 5);
			mDbContext.SaveChanges();

			await mBrowserTabRepository.IncrementTabIndices(mBrowserId, new TabRange(4, 5), incrementBy: -1);

			var tabs = mDbContext.BrowserTabs.AsNoTracking().ToList();

			Assert.That(tabs.Single(x => x.BrowserTabId == 1).Index, Is.EqualTo(1));
			Assert.That(tabs.Single(x => x.BrowserTabId == 2).Index, Is.EqualTo(2));
			Assert.That(tabs.Single(x => x.BrowserTabId == 4).Index, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.BrowserTabId == 5).Index, Is.EqualTo(4));
		}

		[Test]
		public async Task IncrementTabIndices_IncrementsOnlyIndicesOfCorrectBrowser()
		{
			var browser2Id = Guid.NewGuid();
			mDbContext.Browsers.Add(new Browser { Id = browser2Id, Name = "BrowserName" });

			AddBrowserTab(browserId: browser2Id, id: 1, index: 1);
			AddBrowserTab(browserId: mBrowserId, id: 2, index: 2);
			AddBrowserTab(browserId: browser2Id, id: 3, index: 3);
			AddBrowserTab(browserId: mBrowserId, id: 4, index: 4);
			mDbContext.SaveChanges();

			await mBrowserTabRepository.IncrementTabIndices(
				mBrowserId,
				new TabRange(fromIndexInclusive: 1),
				incrementBy: 1);

			var tabs = mDbContext.BrowserTabs.AsNoTracking().ToList();

			Assert.That(tabs.Single(x => x.BrowserTabId == 1).Index, Is.EqualTo(1));
			Assert.That(tabs.Single(x => x.BrowserTabId == 2).Index, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.BrowserTabId == 3).Index, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.BrowserTabId == 4).Index, Is.EqualTo(5));
		}

		private void AddBrowserTab(int id, int index, Guid? browserId = null)
		{
			mDbContext.BrowserTabs.Add(new BrowserTab
			{
				BrowserId = browserId ?? mBrowserId,
				BrowserTabId = id,
				Index = index,
				ServerTabId = mServerTabId
			});
		}
	}
}