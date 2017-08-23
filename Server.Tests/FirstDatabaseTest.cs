using System;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tests.TestTools;

namespace RealTimeTabSynchronizer.Server.Tests
{
	[TestFixtureForAllDatabases]
	public class FirstDatabaseTest
	{
		private readonly TabSynchronizerDbContext mDbContext;
		private IDbContextTransaction mTransaction;

		public FirstDatabaseTest(TabSynchronizerDbContext context)
		{
			// TODO The dbcontext will be shared by all tests... thats bad and it needs disposing...
			// TestFixtureForAllDatabases should only return configuration and the databases should be created
			// elsewhere...
			mDbContext = context;
		}

		[SetUp]
		public void SetUp()
		{
			mTransaction = mDbContext.Database.BeginTransaction();
		}

		[TearDown]
		public void TearDown()
		{
			mTransaction.Dispose();
		}

		[Test]
		public void FirstDatabase()
		{
			var tabDataRepo = new TabDataRepository(mDbContext);

			tabDataRepo.Add(new TabData() { Url = "Test" });

			mDbContext.SaveChanges();

			// TODO Should check in a new context to ensure data saved...

			var tabs = mDbContext.Tabs.ToList();

			Assert.That(tabs.Select(x => x.Url), Is.EquivalentTo(new[] { "Test" }));
		}
	}
}
