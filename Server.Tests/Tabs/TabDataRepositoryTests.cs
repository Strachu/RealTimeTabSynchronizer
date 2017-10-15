using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tests.TestTools;

namespace RealTimeTabSynchronizer.Server.Tests.Tabs
{
	[TestFixtureForAllDatabases]
	public class TabDataRepositoryTests
	{
		private readonly DbContextFactory mCurrentDatabaseContextFactory;

		private ITabDataRepository mTabDataRepository;
		private TabSynchronizerDbContext mDbContext;
		private IDbContextTransaction mTransaction;

		public TabDataRepositoryTests(DbContextFactory contextFactory)
		{
			mCurrentDatabaseContextFactory = contextFactory;
		}

		[SetUp]
		public void SetUp()
		{
			mDbContext = mCurrentDatabaseContextFactory.Create();
			mTransaction = mDbContext.Database.BeginTransaction();

			mTabDataRepository = new TabDataRepository(mDbContext);
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
			AddTab(id: 1, index: 1);
			AddTab(id: 2, index: 2);
			AddTab(id: 3, index: 3);
			AddTab(id: 5, index: 5);
			mDbContext.SaveChanges();

			await mTabDataRepository.IncrementTabIndices(new TabRange(2, 3), incrementBy: 1);

			var tabs = mDbContext.Tabs.AsNoTracking().ToList();

			Assert.That(tabs.Single(x => x.Id == 1).Index, Is.EqualTo(1));
			Assert.That(tabs.Single(x => x.Id == 2).Index, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.Id == 3).Index, Is.EqualTo(4));
			Assert.That(tabs.Single(x => x.Id == 5).Index, Is.EqualTo(5));
		}

		[Test]
		public async Task IncrementTabIndices_DecrementsTheIndicesOfAllTabsInSpecifiedRangeWhenNegativeAmountHasBeenSpecified()
		{
			AddTab(id: 1, index: 1);
			AddTab(id: 2, index: 2);
			AddTab(id: 4, index: 4);
			AddTab(id: 5, index: 5);
			mDbContext.SaveChanges();

			await mTabDataRepository.IncrementTabIndices(new TabRange(4, 5), incrementBy: -1);

			var tabs = mDbContext.Tabs.AsNoTracking().ToList();

			Assert.That(tabs.Single(x => x.Id == 1).Index, Is.EqualTo(1));
			Assert.That(tabs.Single(x => x.Id == 2).Index, Is.EqualTo(2));
			Assert.That(tabs.Single(x => x.Id == 4).Index, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.Id == 5).Index, Is.EqualTo(4));
		}

		[Test]
		public async Task IncrementTabIndices_RefreshesAlreadyLoadedEntities()
		{
			AddTab(id: 1, index: 1);
			AddTab(id: 2, index: 2);
			AddTab(id: 3, index: 3);
			mDbContext.SaveChanges();

			var tabs = mDbContext.Tabs.ToList();

			await mTabDataRepository.IncrementTabIndices(new TabRange(2), incrementBy: 1);

			Assert.That(tabs.Single(x => x.Id == 2).Index, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.Id == 3).Index, Is.EqualTo(4));
		}

		/// LastModificationTime is used to ensure that the specified has been modified.
		/// Incrementing of tab indices is used to update tab index due to a change to a 
		/// another tab, so only the LastModificationTime of another tab should be updated.
		[Test]
		public async Task IncrementTabIndices_DoesNotChangeLastModificationTimeOfIncrementTabIndices()
		{
			var originalModificationTime = new DateTime(2010, 11, 5, 12, 05, 12);

			AddTab(id: 1, index: 1, lastModificationTime: originalModificationTime);
			AddTab(id: 2, index: 2, lastModificationTime: originalModificationTime);
			AddTab(id: 3, index: 3, lastModificationTime: originalModificationTime);
			mDbContext.SaveChanges();

			await mTabDataRepository.IncrementTabIndices(new TabRange(2), incrementBy: 1);
			mDbContext.SaveChanges();

			var tabs = mDbContext.Tabs.AsNoTracking().ToList();

			Assert.That(tabs.Select(x => x.LastModificationTime), Has.All.EqualTo(originalModificationTime));
		}

		private void AddTab(int id, int index, DateTime? lastModificationTime = null)
		{
			mDbContext.Tabs.Add(new TabData
			{
				Id = id,
				Index = index,
				Url = "Url",
				LastModificationTime = lastModificationTime ?? DateTime.Now
			});
		}
	}
}