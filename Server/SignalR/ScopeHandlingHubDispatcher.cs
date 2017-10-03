using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Hubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RealTimeTabSynchronizer.Server.SignalR
{
	public class ScopeHandlingHubDispatcher : HubDispatcher
	{
		private readonly IServiceProvider mServiceProvider;
		private static readonly AsyncLocal<IServiceScope> mCurrentScope = new AsyncLocal<IServiceScope>();

		public ScopeHandlingHubDispatcher(IServiceProvider serviceProvider, IOptions<SignalROptions> options)
						: base(options)
		{
			mServiceProvider = serviceProvider;
		}

		public static IServiceScope CurrentScope => mCurrentScope.Value;

		protected async override Task OnReceived(HttpRequest request, string connectionId, string data)
		{
			using (var scope = mServiceProvider.CreateScope())
			{
				mCurrentScope.Value = scope;

				await base.OnReceived(request, connectionId, data);

				mCurrentScope.Value = null;
			}
		}
	}
}