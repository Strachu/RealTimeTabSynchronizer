using System;

namespace RealTimeTabSynchronizer.Server.Browsers
{
    public class Browser
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return $"{nameof(Id)}: {Id}, {nameof(Name)}: {Name}";
        }
    }
}