using Pacco.Services.Orders.Application;

namespace Pacco.Services.Orders.Infrastructure.Contexts
{
    internal class AppContext : IAppContext
    {
        public string RequestId { get; }
        public IIdentityContext Identity { get; }

        internal AppContext()
        {
            Identity = new IdentityContext();
        }

        internal AppContext(CorrelationContext context)
        {
            RequestId = context.CorrelationId;
            Identity = new IdentityContext(context.User);
        }

        internal AppContext(string requestId, IIdentityContext identity)
        {
            RequestId = requestId;
            Identity = identity;
        }
    }
}