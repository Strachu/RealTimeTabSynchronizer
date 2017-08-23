using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RealTimeTabSynchronizer.Server.EntityFramework;

namespace RealTimeTabSynchronizer.Server.Tests.TestTools
{
	public class DbContextProvider
	{
		private static IDictionary<DatabaseProvider, string> mConnectionStringByDatabaseProvider;
		private static ISet<DatabaseProvider> mInitializedDatabases = new HashSet<DatabaseProvider>();
		private static object mDatabaseInitializationLock = new object();

		static DbContextProvider()
		{
			ParseDatabasesConfiguration();
		}

		public static IEnumerable<TabSynchronizerDbContext> GetForAllSupportedDatabases()
		{
			// TODO Change the new ServiceCollection() to use real configured container
			var modelBuildingService = new ModelBuildingService(new ServiceCollection().BuildServiceProvider());

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

				var dbContext = new TabSynchronizerDbContext(optionsBuilder.Options, modelBuildingService);

				EnsureTestDatabaseUpdated(databaseProvider, dbContext);

				yield return dbContext;
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

		private static void EnsureTestDatabaseUpdated(DatabaseProvider currentProvider, DbContext context)
		{
			lock (mDatabaseInitializationLock)
			{
				if (!mInitializedDatabases.Contains(currentProvider))
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