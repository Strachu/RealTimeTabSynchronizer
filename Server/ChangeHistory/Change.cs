using System;

namespace RealTimeTabSynchronizer.Server.ChangeHistory
{
    public class Change
    {
        public Guid Id { get; set; }

        public Guid BrowserId { get; set; }
    }
}