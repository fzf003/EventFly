﻿using Akka.Actor;
using EventFly.Definitions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace EventFly.DependencyInjection
{
    public sealed class EventFlyBuilder
    {
        public IServiceCollection Services { get; }
        private readonly ActorSystem _actorSystem;
        private readonly ApplicationDefinition _applicationDefinition;
        public IApplicationDefinition ApplicationDefinition => _applicationDefinition;

        public EventFlyBuilder(ActorSystem actorSystem, IServiceCollection services)
        {
            _actorSystem = actorSystem;
            Services = services;
            _applicationDefinition = new ApplicationDefinition();
            Services
                .AddSingleton<IApplicationDefinition>(_applicationDefinition)
                .AddSingleton<IDefinitionToManagerRegistry, DefinitionToManagerRegistry>();
        }

        public EventFlyBuilder WithContext<TContext>()
            where TContext : ContextDefinition, new()
        {
            var context = new TContext();

            context.DI(Services);

            _applicationDefinition.RegisterContext(context);
            return this;
        }
    }
}
