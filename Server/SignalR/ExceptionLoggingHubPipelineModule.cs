using Microsoft.AspNetCore.SignalR.Hubs;
using Microsoft.Extensions.Logging;

namespace RealTimeTabSynchronizer.Server.SignalR
{
    public class ExceptionLoggingHubPipelineModule : HubPipelineModule
    {
        private readonly ILoggerFactory mLoggerFactory;

        public ExceptionLoggingHubPipelineModule(ILoggerFactory loggerFactory)
        {
            mLoggerFactory = loggerFactory;
        }

        protected override void OnIncomingError(
            ExceptionContext exceptionContext, 
            IHubIncomingInvokerContext invokerContext)
        {
            var logger = mLoggerFactory.CreateLogger(invokerContext.Hub.GetType().Name);
            var methodName = invokerContext.MethodDescriptor.Name;

            logger.LogError(
                message: $"[{methodName}]: {exceptionContext.Error.Message}",
                exception: exceptionContext.Error,
                eventId: -1);
        }
    }
}