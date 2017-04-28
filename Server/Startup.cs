using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealTimeTabSynchronizer.Server.EntityFramework;
using RealTimeTabSynchronizer.Server.SignalR;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
            services.AddCors();
            services.AddSignalR();

            services.Configure<DatabaseOptions>(Configuration.GetSection("Database"));

            services.AddSingleton<IHubActivator, ScopeHubActivator>();
            services.AddSingleton<DbContextFactory>();
            services.AddScoped<ITabDataRepository, TabDataRepository>();
            services.AddScoped<IActiveTabDao, ActiveTabDao>();

            services.AddSingleton<Configurator>();
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
