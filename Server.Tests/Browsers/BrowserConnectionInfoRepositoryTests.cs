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
		public async Task GetByBrowserId_ReturnsNullWhenTheBrowserHasNotBeenInitialized()
		{
			var browserId = Guid.NewGuid();

			mConnectionRepository.AddConnection(browserId, "browser");
			mBrowserRepositoryMock.Setup(x => x.GetById(browserId)).Returns(Task.FromResult((Browser)null));

			var connection = await mConnectionRepository.GetByBrowserId(browserId);

			Assert.That(connection, Is.Null);
		}
	}
}