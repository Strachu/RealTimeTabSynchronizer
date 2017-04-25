using System;
using Microsoft.AspNetCore.SignalR.Hubs;
using Microsoft.Extensions.DependencyInjection;

namespace Server.SignalR
{
    /// A hub activator which creates the hubs in current scope allowing the usage of dependencies
    /// scoped per signalR request.
    public class ScopeHubActivator : IHubActivator
	{
        private readonly IServiceProvider mMainServiceProvider;

		public ScopeHubActivator(IServiceProvider serviceProvider)
		{
            mMainServiceProvider = serviceProvider;
		}
        public IHub Create(HubDescriptor descriptor)
        {
            var providerToUse = ScopeHandlingHubDispatcher.CurrentScope?.ServiceProvider ?? mMainServiceProvider;

            return ActivatorUtilities.CreateInstance(providerToUse, descriptor.HubType) as IHub;
        }
	}
}