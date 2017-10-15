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
	[TestFixture]
	public class BrowserConnectionInfoRepositoryTests
	{

		private IBrowserConnectionInfoRepository mConnectionRepository;
		private Mock<IBrowserRepository> mBrowserRepositoryMock;

		[SetUp]
		public void SetUp()
		{
			mBrowserRepositoryMock = new Mock<IBrowserRepository>();

			mBrowserRepositoryMock.Setup(x => x.GetById(It.IsAny<Guid>())).Returns<Guid>(browserId => Task.FromResult(new Browser
			{
				Id = browserId
			}));

			mConnectionRepository = new BrowserConnectionInfoRepository(mBrowserRepositoryMock.Object);
		}

		/// To prevent a situation when AddTab is scheduled for already existing tabs during first 
		/// synchronization of 2 browsers at the same time.
		[Test]
		public async Task GetConnectedBrowsers_DoesNotReturnNotYetInitializedBrowsers()
		{
			var initializedBrowserId = Guid.NewGuid();
			var notInitializedBrowserId = Guid.NewGuid();

			mConnectionRepository.AddConnection(initializedBrowserId, "initializedBrowserId");
			mConnectionRepository.AddConnection(notInitializedBrowserId, "notInitializedBrowserId");

			mBrowserRepositoryMock.Setup(x => x.GetById(initializedBrowserId)).Returns(Task.FromResult(new Browser
			{
				Id = initializedBrowserId
			}));
			mBrowserRepositoryMock.Setup(x => x.GetById(notInitializedBrowserId)).Returns(Task.FromResult((Browser)null));

			var connectedBrowsers = await mConnectionRepository.GetConnectedBrowsers();

			Assert.That(connectedBrowsers.Select(x => x.BrowserId), Is.EquivalentTo(new[] { initializedBrowserId }));
		}
		
		[Test]
		public async Task GetConnectedBrowsers_IteratingTheResultDoesNotThrowExceptionWhenNewConnectionHasComeInMeanTime()
		{
			mConnectionRepository.AddConnection(Guid.NewGuid(), Guid.NewGuid().ToString());
			mConnectionRepository.AddConnection(Guid.NewGuid(), Guid.NewGuid().ToString());

			Assert.DoesNotThrowAsync(async () =>
			{
				var connectedBrowsers = await mConnectionRepository.GetConnectedBrowsers();

				foreach (var browser in connectedBrowsers)
				{
					mConnectionRepository.AddConnection(Guid.NewGuid(), Guid.NewGuid().ToString());
				}
			});
		}
		
		[Test]
		public async Task GetByBrowserId_ReturnsNullWhenTheBrowserHasNotBeenInitialized()
		{
			var browserId = Guid.NewGuid();

			mConnectionRepository.AddConnection(browserId, "browser");
			mBrowserRepositoryMock.Setup(x => x.GetById(browserId)).Returns(Task.FromResult((Browser)null));

			var connection = await mConnectionRepository.GetByBrowserId(browserId);

			Assert.That(connection, Is.Null);
		}

		/// We are using only single connection per browser.
		/// A situation when second connection is added to the server before old one is removed
		/// can happen in sudden loss of a connection, for example when the browser crashes and is
		/// restarted.
		[Test]
		public async Task AddConnection_RemovesOldConnectionForBrowserWhenNewConnectionForTheSameBrowserIsAdded()
		{
			var browserId = Guid.NewGuid();

			mConnectionRepository.AddConnection(browserId, "connection 1");

			mConnectionRepository.AddConnection(browserId, "connection 2");

			var connection = await mConnectionRepository.GetByBrowserId(browserId);

			Assert.That(connection.ConnectionId, Is.EqualTo("connection 2"));
		}
	}
}