using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Hubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Server.SignalR
{
    public class ScopeHandlingHubDispatcher : HubDispatcher
	{
        private readonly IServiceProvider mServiceProvider;

		public ScopeHandlingHubDispatcher(IServiceProvider serviceProvider, IOptions<SignalROptions> options)
            : base(options)
		{
            mServiceProvider = serviceProvider;
		}

        public static IServiceScope CurrentScope { get; private set; }

        protected async override Task OnReceived(HttpRequest request, string connectionId, string data)
        {
            using(var scope = mServiceProvider.CreateScope())
            {
                CurrentScope = scope;

                await base.OnReceived(request, connectionId, data);
                
                CurrentScope = null;
            }
        }
	}
}