using System.Collections.Generic;
using System.Threading.Tasks;
using RealTimeTabSynchronizer.Server.Browsers;
using RealTimeTabSynchronizer.Server.TabData_;

namespace RealTimeTabSynchronizer.Server
{
    public interface IInitializeNewBrowserCommand
    {
        Task ExecuteAsync(IBrowserApi browser, Browser browserInfo, IReadOnlyCollection<TabData> currentlyOpenTabs);
    }
}