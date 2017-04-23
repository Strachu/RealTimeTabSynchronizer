using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace RealTimeTabSynchronizer.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            RecreateDatabase();

            var hostingConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(hostingConfiguration)
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }

        // TODO Change to migrations
        private static void RecreateDatabase()
        {
                using (var uow = new TabSynchronizerDbContext())
                {
                    //uow.Database.EnsureDeleted();
                    uow.Database.EnsureCreated();
                    uow.SaveChanges();
                }
        }
    }
}
