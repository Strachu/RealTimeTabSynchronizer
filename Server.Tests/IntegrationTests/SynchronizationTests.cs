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
using Microsoft.AspNetCore.SignalR.Hubs;
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

			var clientsMock = new Mock<IHubCallerConnectionContext<IBrowserApi>> { DefaultValue = DefaultValue.Mock };
			mSynchronizer.Clients = clientsMock.Object;
			
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
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 1,
						url: ""removed url 1"",
						createInBackground: 0						
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:01:00"",
						index: 1,
						newUrl: ""removed url 2"",				
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""closeTab"",
						dateTime: ""2017-01-01 10:02:00"",
						index: 1,			
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:05:00"",
						index: 2,			
						newIndex: 0,	
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:05:30"",
						index: 2,			
						newUrl: ""transient url"",		
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:06:00"",
						index: 0,			
						newIndex: 2,	
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
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

		[Test]
		public async Task ServerStateIsCorrectAfterAdding2Tabs()
		{
			// 012 -> 3012 -> 30142
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 0,
						url: ""http://www.tab3.com"",
						createInBackground: 0			
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 11:00:00"",
						index: 3,
						url: ""http://www.tab4.com"",
						createInBackground: 1		
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 0, Index = 1, Url = "http://www.tab0.com" },
					new TabData() { Id = 1, Index = 2, Url = "http://www.tab1.com" },
					new TabData() { Id = 2, Index = 4, Url = "http://www.tab2.com" },
					new TabData() { Id = 3, Index = 0, Url = "http://www.tab3.com" },
					new TabData() { Id = 4, Index = 3, Url = "http://www.tab4.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Select(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(5));
			Assert.That(tabs.Single(x => x.Index == 0).Url, Is.EquivalentTo("http://www.tab3.com"));
			Assert.That(tabs.Single(x => x.Index == 1).Url, Is.EquivalentTo("http://www.tab0.com"));
			Assert.That(tabs.Single(x => x.Index == 2).Url, Is.EquivalentTo("http://www.tab1.com"));
			Assert.That(tabs.Single(x => x.Index == 3).Url, Is.EquivalentTo("http://www.tab4.com"));
			Assert.That(tabs.Single(x => x.Index == 4).Url, Is.EquivalentTo("http://www.tab2.com"));
		}

		[Test]
		public async Task ServerStateIsCorrectAfterRemoving2Tabs()
		{
			mDbContext.BrowserTabs.RemoveRange(mDbContext.BrowserTabs);
			mDbContext.Tabs.RemoveRange(mDbContext.Tabs);
			await mDbContext.SaveChangesAsync();
			var serverTabs = new[]
			{
				new TabData { Index = 0, Url = "http://www.tab0.com" },
				new TabData { Index = 1, Url = "http://www.tab1.com" },
				new TabData { Index = 2, Url = "http://www.tab2.com" },
				new TabData { Index = 3, Url = "http://www.tab3.com" },
				new TabData { Index = 4, Url = "http://www.tab4.com" },
				new TabData { Index = 5, Url = "http://www.tab5.com" },
			};

			await AddServerTabs(serverTabs);

			// 012345 -> 02345 -> 0235
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""closeTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 1,				
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""closeTab"",
						dateTime: ""2017-01-01 10:02:00"",
						index: 3,			
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 0, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 2, Index = 1, Url = "http://www.tab2.com" },
					new TabData() { Id = 3, Index = 2, Url = "http://www.tab3.com" },
					new TabData() { Id = 5, Index = 3, Url = "http://www.tab5.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Select(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(4));
			Assert.That(tabs.Single(x => x.Index == 0).Url, Is.EquivalentTo("http://www.tab0.com"));
			Assert.That(tabs.Single(x => x.Index == 1).Url, Is.EquivalentTo("http://www.tab2.com"));
			Assert.That(tabs.Single(x => x.Index == 2).Url, Is.EquivalentTo("http://www.tab3.com"));
			Assert.That(tabs.Single(x => x.Index == 3).Url, Is.EquivalentTo("http://www.tab5.com"));
		}

		[Test]
		public async Task ServerStateIsCorrectAfterChangingUrlOf2Tabs()
		{
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 1,			
						newUrl: ""new url 1"",			
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 2,			
						newUrl: ""new url 2"",
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 0, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 1, Index = 1, Url = "new url 1" },
					new TabData() { Id = 2, Index = 2, Url = "new url 2" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Select(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.Index == 0).Url, Is.EquivalentTo("http://www.tab0.com"));
			Assert.That(tabs.Single(x => x.Index == 1).Url, Is.EquivalentTo("new url 1"));
			Assert.That(tabs.Single(x => x.Index == 2).Url, Is.EquivalentTo("new url 2"));
		}

		[Test]
		public async Task ServerStateIsCorrectAfterMoving2Tabs()
		{
			// 012 -> 120 -> 102
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 0,			
						newIndex: 2,	
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:06:00"",
						index: 1,			
						newIndex: 2,	
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 0, Index = 1, Url = "http://www.tab0.com" },
					new TabData() { Id = 1, Index = 0, Url = "http://www.tab1.com" },
					new TabData() { Id = 2, Index = 2, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Select(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(3));
			Assert.That(tabs.Single(x => x.Index == 0).Url, Is.EquivalentTo("http://www.tab1.com"));
			Assert.That(tabs.Single(x => x.Index == 1).Url, Is.EquivalentTo("http://www.tab0.com"));
			Assert.That(tabs.Single(x => x.Index == 2).Url, Is.EquivalentTo("http://www.tab2.com"));
		}

		[Test]
		public async Task ServerStateIsCorrectAfterApplyingManyDifferentActions()
		{
			// 012 -> 0312 -> 3102 -> 31024 -> 30214 -> 3014
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 1,
						url: ""http://www.tab3.com"",
						createInBackground: 0						
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:05:00"",
						index: 0,			
						newIndex: 2,	
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 10:10:00"",
						index: 4,
						url: ""http://www.tab4.com"",
						createInBackground: 0								
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""moveTab"",
						dateTime: ""2017-01-01 10:15:00"",
						index: 1,			
						newIndex: 3,	
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""changeTabUrl"",
						dateTime: ""2017-01-01 10:25:30"",
						index: 3,			
						newUrl: ""new url"",		
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""closeTab"",
						dateTime: ""2017-01-01 10:30:00"",
						index: 2,			
					}"),
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 0, Index = 1, Url = "http://www.tab0.com" },
					new TabData() { Id = 1, Index = 2, Url = "new url" },
					new TabData() { Id = 3, Index = 0, Url = "http://www.tab3.com" },
					new TabData() { Id = 4, Index = 3, Url = "http://www.tab4.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Select(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(4));
			Assert.That(tabs.Single(x => x.Index == 0).Url, Is.EquivalentTo("http://www.tab3.com"));
			Assert.That(tabs.Single(x => x.Index == 1).Url, Is.EquivalentTo("http://www.tab0.com"));
			Assert.That(tabs.Single(x => x.Index == 2).Url, Is.EquivalentTo("new url"));
			Assert.That(tabs.Single(x => x.Index == 3).Url, Is.EquivalentTo("http://www.tab4.com"));
		}

		[Test]
		public async Task WhenTabIsAddedAfterATabWhichHasBeenCreatedAndRemovedInTheSameDisconnectedSession_TheIndexOfTheTabIsCorrect()
		{
			// 012 -> 0123 -> 01234 -> 0124
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 3,
						url: ""http://www.tab2.com"",
						createInBackground: 0						
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 11:00:00"",
						index: 4,
						url: ""http://www.tab4.com"",
						createInBackground: 0						
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""closeTab"",
						dateTime: ""2017-01-01 12:00:00"",
						index: 3,			
					}"),
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 0, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 1, Index = 1, Url = "http://www.tab1.com" },
					new TabData() { Id = 2, Index = 2, Url = "http://www.tab2.com" },
					new TabData() { Id = 4, Index = 3, Url = "http://www.tab4.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().ToList();
			var addedTab = tabs.Single(x => x.BrowserTabId == 4);

			Assert.That(addedTab.Index, Is.EqualTo(3));
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
						changeId: """+Guid.NewGuid()+@""",
						type: ""createTab"",
						dateTime: ""2017-01-01 10:00:00"",
						index: 1,
						url: ""http://newtab"",
						createInBackground: 0						
					}"),
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
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
						changeId: """+Guid.NewGuid()+@""",
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

		// At least Firefox Android assigns ids starting at 0 which can be problematic when ids of closed
		// tabs are negated, as 0 negated is still 0 thus making a tabs with duplicated ids before
		// they are removed from the server.
		[Test]
		public async Task BrowserTabsIdAreUpdatedCorrectly_WhenFirstTabWithIdEqualToZeroHasBeenClosedAndTheZeroIdHasBeenAssignedToDifferentBrowser()
		{
			mDbContext.BrowserTabs.Single(x => x.Index == 0).BrowserTabId = 0;
			mDbContext.SaveChanges();

			// 012 -> 12
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
						type: ""closeTab"",
						dateTime: ""2017-01-01 10:02:00"",
						index: 0,						
					}")
				},
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 0, Index = 0, Url = "http://www.tab1.com" },
					new TabData() { Id = 1, Index = 1, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Include(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(2));
			AssertTabDataCorrect(tabs.Single(x => x.Index == 0), tabId: 0, url: "http://www.tab1.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 1), tabId: 1, url: "http://www.tab2.com");
		}

		[Test]
		public async Task BrowserTabsIdAreUpdatedCorrectly_WhenTabHasBeenMoved()
		{
			// 012 -> 120
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[]
				{
					JObject.Parse(@"{
						changeId: """+Guid.NewGuid()+@""",
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

		// Databases like to throw Unique contraint violation in this situation regardless of the fact
		// that after executing all updates no violation will occur.
		[Test]
		public async Task BrowserTabsIdAreUpdatedWithoutException_EvenWhenJustOrderOfIdChanges()
		{
			await mSynchronizer.Synchronize(mBrowserId,
				changesSinceLastConnection: new object[0],
				currentlyOpenTabs: new TabData[]
				{
					new TabData() { Id = 101, Index = 0, Url = "http://www.tab0.com" },
					new TabData() { Id = 100, Index = 1, Url = "http://www.tab1.com" },
					new TabData() { Id = 102, Index = 2, Url = "http://www.tab2.com" },
				});

			var tabs = mDbContext.BrowserTabs.AsNoTracking().Include(x => x.ServerTab).ToList();

			Assert.That(tabs.Count, Is.EqualTo(3));
			AssertTabDataCorrect(tabs.Single(x => x.Index == 0), tabId: 101, url: "http://www.tab0.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 1), tabId: 100, url: "http://www.tab1.com");
			AssertTabDataCorrect(tabs.Single(x => x.Index == 2), tabId: 102, url: "http://www.tab2.com");
		}

		private async Task AddServerTabs(IEnumerable<TabData> serverTabs)
		{
			foreach (var tab in serverTabs)
			{
				mDbContext.Tabs.Add(tab);
				mDbContext.BrowserTabs.Add(new BrowserTab
				{
					BrowserId = mBrowserId,
					BrowserTabId = tab.Index.Value,
					Index = tab.Index.Value,
					Url = tab.Url,
					ServerTab = tab
				});
			}

			await mDbContext.SaveChangesAsync();
		}

		private void AssertTabDataCorrect(BrowserTab tab, int tabId, string url)
		{
			Assert.That(tab.BrowserTabId, Is.EqualTo(tabId));
			Assert.That(tab.ServerTab.Url, Is.EqualTo(url));
		}
	}
}