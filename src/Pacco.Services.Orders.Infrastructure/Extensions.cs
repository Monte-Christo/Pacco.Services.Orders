using System;
using System.Linq;
using System.Text;
using Convey;
using Convey.CQRS.Commands;
using Convey.CQRS.Events;
using Convey.CQRS.Queries;
using Convey.Discovery.Consul;
using Convey.Docs.Swagger;
using Convey.HTTP;
using Convey.LoadBalancing.Fabio;
using Convey.MessageBrokers;
using Convey.MessageBrokers.CQRS;
using Convey.MessageBrokers.Inbox;
using Convey.MessageBrokers.Outbox;
using Convey.MessageBrokers.RabbitMQ;
using Convey.Metrics.AppMetrics;
using Convey.Persistence.MongoDB;
using Convey.Persistence.Redis;
using Convey.Tracing.Jaeger;
using Convey.Tracing.Jaeger.RabbitMQ;
using Convey.WebApi;
using Convey.WebApi.CQRS;
using Convey.WebApi.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Pacco.Services.Orders.Application;
using Pacco.Services.Orders.Application.Commands;
using Pacco.Services.Orders.Application.Events.External;
using Pacco.Services.Orders.Application.Services;
using Pacco.Services.Orders.Application.Services.Clients;
using Pacco.Services.Orders.Core.Repositories;
using Pacco.Services.Orders.Infrastructure.Contexts;
using Pacco.Services.Orders.Infrastructure.Decorators;
using Pacco.Services.Orders.Infrastructure.Exceptions;
using Pacco.Services.Orders.Infrastructure.Logging;
using Pacco.Services.Orders.Infrastructure.Mongo.Documents;
using Pacco.Services.Orders.Infrastructure.Mongo.Repositories;
using Pacco.Services.Orders.Infrastructure.Services;
using Pacco.Services.Orders.Infrastructure.Services.Clients;

namespace Pacco.Services.Orders.Infrastructure
{
    public static class Extensions
    {
        public static IConveyBuilder AddInfrastructure(this IConveyBuilder builder)
        {
            builder.Services.AddOpenTracing();
            builder.Services.AddSingleton<IEventMapper, EventMapper>();
            builder.Services.AddTransient<IMessageBroker, MessageBroker>();
            builder.Services.AddTransient<ICustomerRepository, CustomerMongoRepository>();
            builder.Services.AddTransient<IOrderRepository, OrderMongoRepository>();
            builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            builder.Services.AddTransient<IParcelsServiceClient, ParcelsServiceClient>();
            builder.Services.AddTransient<IPricingServiceClient, PricingServiceClient>();
            builder.Services.AddTransient<IVehiclesServiceClient, VehiclesServiceClient>();
            builder.Services.AddTransient<IAppContextFactory, AppContextFactory>();
            builder.Services.AddTransient(ctx => ctx.GetRequiredService<IAppContextFactory>().Create());
            builder.Services.TryDecorate(typeof(ICommandHandler<>), typeof(InboxCommandHandlerDecorator<>));
            builder.Services.TryDecorate(typeof(IEventHandler<>), typeof(InboxEventHandlerDecorator<>));

            return builder
                .AddQueryHandlers()
                .AddInMemoryQueryDispatcher()
                .AddHttpClient()
                .AddConsul()
                .AddFabio()
                .AddRabbitMq(plugins: p => p.AddJaegerRabbitMqPlugin())
                .AddMessageInbox()
                .AddMessageOutbox()
                .AddExceptionToMessageMapper<ExceptionToMessageMapper>()
                .AddMongo()
                .AddRedis()
                .AddMetrics()
                .AddJaeger()
                .AddHandlersLogging()
                .AddMongoRepository<CustomerDocument, Guid>("Customers")
                .AddMongoRepository<OrderDocument, Guid>("Orders")
                .AddWebApiSwaggerDocs();
        }

        public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
        {
            app.UseErrorHandler()
                .UseSwaggerDocs()
                .UseJaeger()
                .UseConvey()
                .UsePublicContracts<ContractAttribute>()
                .UseMetrics()
                .UseRabbitMq()
                .SubscribeCommand<ApproveOrder>()
                .SubscribeCommand<CreateOrder>()
                .SubscribeCommand<CancelOrder>()
                .SubscribeCommand<DeleteOrder>()
                .SubscribeCommand<AddParcelToOrder>()
                .SubscribeCommand<DeleteParcelFromOrder>()
                .SubscribeCommand<AssignVehicleToOrder>()
                .SubscribeEvent<CustomerCreated>()
                .SubscribeEvent<DeliveryCompleted>()
                .SubscribeEvent<DeliveryFailed>()
                .SubscribeEvent<DeliveryStarted>()
                .SubscribeEvent<ParcelDeleted>()
                .SubscribeEvent<ResourceReserved>()
                .SubscribeEvent<ResourceReservationCanceled>();

            return app;
        }

        internal static CorrelationContext GetCorrelationContext(this IHttpContextAccessor accessor)
            => accessor.HttpContext?.Request.Headers.TryGetValue("Correlation-Context", out var json) is true
                ? JsonConvert.DeserializeObject<CorrelationContext>(json.FirstOrDefault())
                : null;
        
        internal static string GetSpanContext(this IMessageProperties messageProperties, string header)
        {
            if (messageProperties is null)
            {
                return string.Empty;
            }

            if (messageProperties.Headers.TryGetValue(header, out var span) && span is byte[] spanBytes)
            {
                return Encoding.UTF8.GetString(spanBytes);
            }

            return string.Empty;
        }
    }
}