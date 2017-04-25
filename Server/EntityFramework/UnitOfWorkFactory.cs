using Microsoft.EntityFrameworkCore;

namespace Server.EntityFramework
{
    public class DbContextFactory
    {
        private readonly DbContextOptions<TabSynchronizerDbContext> mContextOptions;

        public DbContextFactory(DbContextOptions<TabSynchronizerDbContext> contextOptions)
        {
            mContextOptions = contextOptions;
        }

        public TabSynchronizerDbContext Create()
        {
            return new TabSynchronizerDbContext(mContextOptions);
        }
    }
}