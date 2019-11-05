﻿using Akka.Actor;
using EventFly.Commands;
using EventFly.Jobs;
using EventFly.Definitions;
using EventFly.Queries;
using EventFly.Schedulers;
using Microsoft.Extensions.DependencyInjection;
using System;
using EventFly.Permissions;
using EventFly.Core;
using EventFly.Aggregates;

namespace EventFly.DependencyInjection
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceProvider UseEventFly(this IServiceProvider serviceProvider)
        {
            var actorSystem = serviceProvider.GetService<ActorSystem>();
            actorSystem.RegisterDependencyResolver(serviceProvider);
            var wow = serviceProvider.GetService<IDefinitionToManagerRegistry>() as DefinitionToManagerRegistry;

            if (wow == null) throw new InvalidOperationException("");

            return serviceProvider;
        }

        public static EventFlyBuilder AddEventFly(
            this IServiceCollection services, string systemName)
        {
            var actorSystem = ActorSystem.Create(systemName);

            return services.AddEventFly(actorSystem);
        }

        public static EventFlyBuilder AddEventFly(
            this IServiceCollection services,
            ActorSystem actorSystem)
        {

            services.AddSingleton(actorSystem);

            services.AddTransient<ISerializedCommandPublisher, SerializedCommandPublisher>();
            services.AddTransient<ISerializedQueryExecutor, SerializedQueryExecutor>();

            services.AddTransient<ICommandBus, CommandToAggregateManagerBus>();
            services.AddTransient<IQueryProcessor, QueryToQueryHandlerProcessor>();
            services.AddTransient<Jobs.IScheduler, JobToJobMannagerScheduler>();
            services.AddTransient<ICommandsScheduler, JobCommandsScheduler>();
            services.AddTransient<IEventsScheduler, JobEventsScheduler>();

            services.AddSingleton<ICommandValidator, DefaultCommandValidator>();
            services.AddSingleton<ICommandValidator, PermissionCommandValidator>();
            services.AddSingleton<ISecurityService, SecurityService>();

            return new EventFlyBuilder(services);
        }

        public static IServiceCollection AddAsyncCommandHandler<TAggregate, TIdentity, TCommand, TCommandHandler>(this IServiceCollection services)
            where TAggregate : ActorBase, IAggregateRoot<TIdentity>
            where TIdentity : IIdentity
            where TCommand : ICommand<TIdentity>
            where TCommandHandler: AsyncCommandHandler<TAggregate, TIdentity, TCommand>
        {
            services.AddScoped<AsyncCommandHandler<TAggregate, TIdentity, TCommand>, TCommandHandler>();
            return services;
        }

        public static IServiceCollection AddCommandHandler<TAggregate, TIdentity, TCommand, TCommandHandler>(this IServiceCollection services)
            where TAggregate : ActorBase, IAggregateRoot<TIdentity>
            where TIdentity : IIdentity
            where TCommand : ICommand<TIdentity>
            where TCommandHandler : CommandHandler<TAggregate, TIdentity, TCommand>
        {
            services.AddScoped<CommandHandler<TAggregate, TIdentity, TCommand>, TCommandHandler>();
            return services;
        }
    }
}
