using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace RealTimeTabSynchronizer.Server.EntityFramework
{
    public class Configurator
    {
        private readonly TimeSpan Timeout = TimeSpan.FromMinutes(5); 

        private readonly IOptions<DatabaseOptions> mOptions;

        public Configurator(IOptions<DatabaseOptions> options)
        {
            mOptions = options;
        }

        public void Configure(DbContextOptionsBuilder EfOptions)
        {
            switch(mOptions.Value.DatabaseType)
            {
                case DatabaseProvider.Sqlite:
                    EfOptions.UseSqlite(mOptions.Value.ConnectionString, x => x.CommandTimeout((int)Timeout.TotalSeconds));
                    break;

                case DatabaseProvider.Postgresql:
                    EfOptions.UseNpgsql(mOptions.Value.ConnectionString, x => x.CommandTimeout((int)Timeout.TotalSeconds));
                    break;

                default:
                {
                    var validValues = $"\"{String.Join("\",\"", Enum.GetNames(typeof(DatabaseProvider)))}\"";
                    throw new InvalidOperationException($"Not valid Database Type. Valid values are: {validValues}");
                }
            }
        }
    }
}