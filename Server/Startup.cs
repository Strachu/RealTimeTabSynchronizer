using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Hubs;
using Microsoft.AspNetCore.SignalR.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealTimeTabSynchronizer.Server.Acknowledgments;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.DiffCalculation;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.SignalR;
using RealTimeTabSynchronizer.Server.TabData_;
using RealTimeTabSynchronizer.Server.TabData_.ClientToServerIdMapping;
using RealTimeTabSynchronizer.Server.Tabs.Browsers;

namespace RealTimeTabSynchronizer.Server
{
	public class Startup
	{
		private readonly IHostingEnvironment mEnvironment;

		public Startup(IHostingEnvironment env)
		{
			var builder = new ConfigurationBuilder()
				 .SetBasePath(env.ContentRootPath)
				 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				 .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				 .AddEnvironmentVariables();
			Configuration = builder.Build();

			mEnvironment = env;
		}

		public Startup(IHostingEnvironment env, IConfigurationRoot configuration)
		{
			mEnvironment = env;
			Configuration = configuration;
		}

		public IConfigurationRoot Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddLogging();
			services.AddCors();
			services.AddSignalR(x => x.Hubs.EnableDetailedErrors = mEnvironment.IsDevelopment());

			services.Configure<DatabaseOptions>(Configuration.GetSection("Database"));

			services.AddSingleton<IHubActivator, ScopeHubActivator>();
			services.AddTransient<IHubContext<SynchronizerHub, IBrowserApi>>(
				x => x.GetService<IConnectionManager>().GetHubContext<SynchronizerHub, IBrowserApi>());

			services.AddSingleton<DbContextFactory>();
			services.AddScoped<ITabDataRepository, TabDataRepository>();
			services.AddScoped<IBrowserRepository, BrowserRepository>();
			services.AddScoped<IBrowserTabRepository, BrowserTabRepository>();
			services.AddScoped<IActiveTabDao, ActiveTabDao>();
			services.AddScoped<IBrowserTabIdServerTabIdMapper, BrowserTabIdServerTabIdMapper>();
			services.AddScoped<IBrowserConnectionInfoRepository, BrowserConnectionInfoRepository>();
			services.AddScoped<ITabService, TabService>();
			services.AddScoped<IPendingRequestService, PendingRequestService>();
			services.AddScoped<IBrowserService, BrowserService>();
			services.AddSingleton<ITabActionDeserializer, JsonTabActionDeserializer>();
			services.AddSingleton<IIndexCalculator, IndexCalculator>();
			services.AddSingleton<IDiffCalculator, DiffCalculator>();
			services.AddSingleton<IChangeListOptimizer, ChangeListOptimizer>();
			services.AddScoped<IInitializeNewBrowserCommand, InitializeNewBrowserCommand>();

			services.AddSingleton<Configurator>();
			services.AddSingleton<IModelBuildingService>(x => new ModelBuildingService(x, typeof(Startup).GetTypeInfo().Assembly));
			services.AddDbContext<TabSynchronizerDbContext>((provider, opts) => provider.GetRequiredService<Configurator>().Configure(opts));
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();

			app.Map("/signalr", map =>
			{
				map.UseCors(x =>
					{
						// x.SetIsOriginAllowed(y =>
						// {
						//     // TODO
						//     return true;
						//     //return Regex.IsMatch(y, "moz-extension://*") ||
						//     //    Regex.IsMatch(y, "^192.168.0.*$");
						// })
						x.AllowAnyOrigin()
							 .AllowCredentials();
					});

				map.RunSignalR<ScopeHandlingHubDispatcher>();
			});
		}
	}
}
