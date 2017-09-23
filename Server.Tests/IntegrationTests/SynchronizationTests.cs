using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.Tests.TestTools;
using RealTimeTabSynchronizer.Server.Browsers;
using System;
using System.Collections.Generic;
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
	public class SynchronizationTests
	{
		private readonly DbContextFactory mCurrentDatabaseContextFactory;

		private SynchronizerHub mSynchronizer;
		private TabSynchronizerDbContext mDbContext;
		private IDbContextTransaction mTransaction;

		private Guid mBrowserId;

		public SynchronizationTests(DbContextFactory contextFactory)
		{
			mCurrentDatabaseContextFactory = contextFactory;
		}

		[SetUp]
		public void SetUp()
		{
			mDbContext = mCurrentDatabaseContextFactory.Create();
			// Cannot nest transactions...
			//	mTransaction = mDbContext.Database.BeginTransaction();

			// TODO Running tests in transaction would be far more performant but transactions cannot be nested...
			// needs to mock them. TransactionScope is sorely missing...
			mDbContext.Database.EnsureDeleted();
			mDbContext.Database.EnsureCreated();

			var container = new ServiceCollection();
			var environmentMock = new Mock<IHostingEnvironment>();
			environmentMock.Setup(x => x.ApplicationName).Returns("RealTimeTabSynchronizer.Server");
			var configurationMock = new Mock<IConfigurationRoot>();
			configurationMock.Setup(x => x.GetSection(It.IsAny<string>())).Returns(new Mock<IConfigurationSection>().Object);
			new Startup(environmentMock.Object, configurationMock.Object).ConfigureServices(container);

			container.Remove(container.Single(x => x.ServiceType == typeof(TabSynchronizerDbContext)));
			container.AddSingleton<TabSynchronizerDbContext>(mDbContext);
			container.AddSingleton<IHostingEnvironment>(environmentMock.Object);
			container.AddSingleton<SynchronizerHub, SynchronizerHub>();

			mSynchronizer = container.BuildServiceProvider().GetRequiredService<SynchronizerHub>();

			mBrowserId = Guid.NewGuid();

			mDbContext.Browsers.Add(new Browser { Id = mBrowserId, Name = "BrowserName" });

			var serverTabs = new[]
			{
				new TabData { Index = 0, Url = "http://www.tab0.com" },
				new TabData { Index = 1, Url = "http://www.tab1.com" },
				new TabData { Index = 2, Url = "http://www.tab2.com" },
			};

			foreach (var tab in serverTabs)
			{
				mDbContext.Tabs.Add(tab);
				mDbContext.BrowserTabs.Add(new BrowserTab
				{
					BrowserId = mBrowserId,
					BrowserTabId = 100 + tab.Index.Value,
					Index = tab.Index.Value,
					Url = tab.Url,
					ServerTab = tab
				});
			}

			mDbContext.SaveChanges();
		}

		[TearDown]
		public void TearDown()
		{
			//	mTransaction.Dispose();
			mDbContext.Dispose();
		}

		[Test]
		public async Task ServerStateDoesNotChangeIfNothingChangedOnTheBrowser()
		{
			await mSynchronizer.Synchronize(mBrowserId, new object[0],
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 100, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 101, Index = 1, Url = "http://www.tab1.com" },
					new TabData() { Id = 102, Index = 2, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Select(x => x.ServerTab).ToList();

			Assert.That(tabs.Single(x => x.Index == 0).Url, Is.EquivalentTo("http://www.tab0.com"));
			Assert.That(tabs.Single(x => x.Index == 1).Url, Is.EquivalentTo("http://www.tab1.com"));
			Assert.That(tabs.Single(x => x.Index == 2).Url, Is.EquivalentTo("http://www.tab2.com"));
		}

		[Test]
		public async Task ServerStateRemainsIntactWhenManyChangesHasBeenDoneButTheyHaveBeenReverted()
		{
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						type: ""createTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 1,
						url: ""removed url 1"",
						createInBackground: 0						
					}"),
					JObject.Parse(@"{
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:01:00"",
						index: 1,
						newUrl: ""removed url 2"",				
					}"),
					JObject.Parse(@"{
						type: ""closeTab"",
						dateTime: ""2017-01-01 10:02:00"",
						index: 1,			
					}"),
					JObject.Parse(@"{
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:05:00"",
						index: 2,			
						newIndex: 0,	
					}"),
					JObject.Parse(@"{
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:05:30"",
						index: 2,			
						newUrl: ""transient url"",		
					}"),
					JObject.Parse(@"{
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:06:00"",
						index: 0,			
						newIndex: 2,	
					}"),
					JObject.Parse(@"{
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:06:30"",
						index: 1,			
						newUrl: ""http://www.tab1.com"",		
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 100, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 101, Index = 1, Url = "http://www.tab1.com" },
					new TabData() { Id = 102, Index = 2, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Select(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.Index == 0).Url, Is.EquivalentTo("http://www.tab0.com"));
			Assert.That(tabs.Single(x => x.Index == 1).Url, Is.EquivalentTo("http://www.tab1.com"));
			Assert.That(tabs.Single(x => x.Index == 2).Url, Is.EquivalentTo("http://www.tab2.com"));
		}

		/// <summary>
		/// At least Firefox resets the tab ids during every restart of the browser.
		/// </summary>
		[Test]
		public async Task BrowserTabsIdAreUpdated_EvenWhenNothingChanged()
		{
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[0],
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 1, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 2, Index = 1, Url = "http://www.tab1.com" },
					new TabData() { Id = 3, Index = 2, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Include(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(3));
			AssertTabDataCorrect(tabs.Single(x => x.Index == 0), tabId: 1, url: "http://www.tab0.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 1), tabId: 2, url: "http://www.tab1.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 2), tabId: 3, url: "http://www.tab2.com");
		}	
		
		[Test]
		public async Task BrowserTabsIdAreUpdatedCorrectly_WhenTabHasBeenMovedServerSide()
		{
			mDbContext.Tabs.Single(x => x.Url == "http://www.tab2.com").Index = Int32.MaxValue;
			await mDbContext.SaveChangesAsync();
			mDbContext.Tabs.Single(x => x.Url == "http://www.tab1.com").Index = 2;
			mDbContext.Tabs.Single(x => x.Url == "http://www.tab2.com").Index = 1;
			await mDbContext.SaveChangesAsync();
			
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[0],
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 1, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 2, Index = 1, Url = "http://www.tab1.com" },
					new TabData() { Id = 3, Index = 2, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Include(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(3));
			AssertTabDataCorrect(tabs.Single(x => x.Index == 0), tabId: 1, url: "http://www.tab0.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 1), tabId: 2, url: "http://www.tab1.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 2), tabId: 3, url: "http://www.tab2.com");
		}
		
		[Test]
		public async Task BrowserTabsIdAreUpdatedCorrectly_WhenNewTabsHaveBeenAdded()
		{
			// 012 -> 0X1X2
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						type: ""createTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 1,
						url: ""http://newtab"",
						createInBackground: 0						
					}"),
					JObject.Parse(@"{
						type: ""createTab"",
						dateTime: ""2017-01-01 11:00:00"",
						index: 3,
						url: ""http://newtab"",
						createInBackground: 1		
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 1, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 900, Index = 1, Url = "http://newtab" },
					new TabData() { Id = 2, Index = 2, Url = "http://www.tab1.com" },
					new TabData() { Id = 901, Index = 3, Url = "http://newtab" },
					new TabData() { Id = 3, Index = 4, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Include(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(5));
			AssertTabDataCorrect(tabs.Single(x => x.Index == 0), tabId: 1, url: "http://www.tab0.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 1), tabId: 900, url: "http://newtab");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 2), tabId: 2, url: "http://www.tab1.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 3), tabId: 901, url: "http://newtab");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 4), tabId: 3, url: "http://www.tab2.com");
		}		
		
		[Test]
		public async Task BrowserTabsIdAreUpdatedCorrectly_WhenTabHasBeenClosed()
		{
			// 012 -> 02
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						type: ""closeTab"",
						dateTime: ""2017-01-01 10:02:00"",
						index: 1,						
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 1, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 3, Index = 1, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Include(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(2));
			AssertTabDataCorrect(tabs.Single(x => x.Index == 0), tabId: 1, url: "http://www.tab0.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 1), tabId: 3, url: "http://www.tab2.com");
		}		
		
		[Test]
		public async Task BrowserTabsIdAreUpdatedCorrectly_WhenTabHasBeenMoved()
		{
			// 012 -> 120
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 0,			
						newIndex: 2,	
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 2, Index = 0, Url = "http://www.tab1.com" },
					new TabData() { Id = 3, Index = 1, Url = "http://www.tab2.com" },
					new TabData() { Id = 1, Index = 2, Url = "http://www.tab0.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Include(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(3));
			AssertTabDataCorrect(tabs.Single(x => x.Index == 0), tabId: 2, url: "http://www.tab1.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 1), tabId: 3, url: "http://www.tab2.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 2), tabId: 1, url: "http://www.tab0.com");
		}	

		private void AssertTabDataCorrect(BrowserTab tab, int tabId, string url)
		{
			Assert.That(tab.BrowserTabId, Is.EqualTo(tabId));
			Assert.That(tab.ServerTab.Url, Is.EqualTo(url));
		}
	}
}