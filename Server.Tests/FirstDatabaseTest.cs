using System;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tests.TestTools;
using Server.Tests.TestTools;

namespace RealTimeTabSynchronizer.Server.Tests
{
	[TestFixtureForAllDatabases]
	public class FirstDatabaseTest
	{
		private readonly DbContextFactory mCurrentDatabaseContextFactory;

		private TabSynchronizerDbContext mDbContext;
		private IDbContextTransaction mTransaction;

		public FirstDatabaseTest(DbContextFactory contextFactory)
		{
			mCurrentDatabaseContextFactory = contextFactory;
		}

		[SetUp]
		public void SetUp()
		{
			mDbContext = mCurrentDatabaseContextFactory.Create();
			mTransaction = mDbContext.Database.BeginTransaction();
		}

		[TearDown]
		public void TearDown()
		{
			mTransaction.Dispose();
			mDbContext.Dispose();
		}

		[Test]
		public void FirstDatabase()
		{
			var tabDataRepo = new TabDataRepository(mDbContext);

			tabDataRepo.Add(new TabData() { Url = "Test" });

			mDbContext.SaveChanges();

			var tabs = mDbContext.Tabs.ToList();

			Assert.That(tabs.Select(x => x.Url), Is.EquivalentTo(new[] { "Test" }));
		}
	}
}
