using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RealTimeTabSynchronizer.Server.EntityFramework;

namespace RealTimeTabSynchronizer.Server.Tests.TestTools
{
	public class DbContextFactoryProvider
	{
		private static IDictionary<DatabaseProvider, string> mConnectionStringByDatabaseProvider;
		private static ISet<DatabaseProvider> mInitializedDatabases = new HashSet<DatabaseProvider>();
		private static object mDatabaseInitializationLock = new object();

		static DbContextFactoryProvider()
		{
			ParseDatabasesConfiguration();
		}

		public static IEnumerable<DbContextFactory> GetForAllSupportedDatabases()
		{
			var container = new ServiceCollection();
			var configurationMock = new Mock<IConfigurationRoot>();
			configurationMock.Setup(x => x.GetSection(It.IsAny<string>())).Returns(new Mock<IConfigurationSection>().Object);
			new Startup(new Mock<IHostingEnvironment>().Object, configurationMock.Object).ConfigureServices(container);

			var modelBuildingService = container.BuildServiceProvider().GetRequiredService<IModelBuildingService>();

			foreach (DatabaseProvider databaseProvider in Enum.GetValues(typeof(DatabaseProvider)))
			{
				if (!mConnectionStringByDatabaseProvider.TryGetValue(databaseProvider, out var connectionString))
				{
					continue;
				}

				var efConfigurer = new Configurator(Options.Create(new DatabaseOptions()
				{
					DatabaseType = databaseProvider,
					ConnectionString = connectionString
				}));

				var optionsBuilder = new DbContextOptionsBuilder<TabSynchronizerDbContext>();
				efConfigurer.Configure(optionsBuilder);

				var dbContextFactory = new DbContextFactory(optionsBuilder.Options, modelBuildingService);

				EnsureTestDatabaseUpdated(databaseProvider, dbContextFactory);

				yield return dbContextFactory;
			}
		}

		private static void ParseDatabasesConfiguration()
		{
			var configuration = new ConfigurationBuilder()
				.AddJsonFile("testsettings.json", optional: false, reloadOnChange: true)
				.Build();

			var providersConfiguration = configuration
				.GetSection("Databases")
				.GetChildren()
				.Select(x => new
				{
					DatabaseProvider = (DatabaseProvider)Enum.Parse(typeof(DatabaseProvider), x.Key),
					Configuration = x
				});

			mConnectionStringByDatabaseProvider = providersConfiguration
				.ToDictionary(x => x.DatabaseProvider, x => x.Configuration.GetValue<string>("ConnectionString"));
		}

		private static void EnsureTestDatabaseUpdated(DatabaseProvider currentProvider, DbContextFactory contextFactory)
		{
			lock (mDatabaseInitializationLock)
			{
				if (!mInitializedDatabases.Contains(currentProvider))
				{
					using (var context = contextFactory.Create())
					{
						context.Database.EnsureDeleted();
						context.Database.EnsureCreated();
						context.SaveChanges();

						mInitializedDatabases.Add(currentProvider);
					}
				}
			}
		}
	}
}