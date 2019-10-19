﻿using System.Collections.Generic;
using Akka.Actor;
using EventFly.Aggregates;
using EventFly.Extensions;
using EventFly.Queries;
using EventFly.Sagas.AggregateSaga;

namespace EventFly.Definitions
{
    public sealed class DefinitionToManagerRegistryBuilder
    {
        private ActorSystem System;

        private IReadOnlyDictionary<IAggregateManagerDefinition, IActorRef> DefinitionToAggregateManager { get; set; } = new Dictionary<IAggregateManagerDefinition, IActorRef>();
        private IReadOnlyDictionary<IQueryManagerDefinition, IActorRef> DefinitionToQueryManager { get; set; } = new Dictionary<IQueryManagerDefinition, IActorRef>();
        private IReadOnlyDictionary<ISagaManagerDefinition, IActorRef> DefinitionToSagaManager { get; set; } = new Dictionary<ISagaManagerDefinition, IActorRef>();
        private IReadOnlyDictionary<IReadModelManagerDefinition, IActorRef> DefinitionToReadModelManager { get; set; } = new Dictionary<IReadModelManagerDefinition, IActorRef>();

        public DefinitionToManagerRegistryBuilder UseSystem(ActorSystem actorSystem)
        {
            System = actorSystem;

            return this;
        }
        public DefinitionToManagerRegistryBuilder RegisterAggregateManagers(IReadOnlyCollection<IAggregateManagerDefinition> definitions)
        {
            var dictionaryAggregate = new Dictionary<IAggregateManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var type = typeof(AggregateManager<,>);
                var generics = type.MakeGenericType(new[] { managerDef.AggregateType, managerDef.IdentityType });

                var manager = System.ActorOf(
                    Props.Create(generics),
                    $"aggregate-{managerDef.AggregateType.GetAggregateName()}-manager"
                );
                dictionaryAggregate.Add(managerDef, manager);
            }

            DefinitionToAggregateManager = dictionaryAggregate;
            return this;
        }

        public DefinitionToManagerRegistryBuilder RegisterQueryManagers(IReadOnlyCollection<IQueryManagerDefinition> definitions)
        {
            var dictionaryQuery = new Dictionary<IQueryManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var type = typeof(QueryManager<,,>);
                var generics = type.MakeGenericType(new[] { managerDef.QueryHandlerType, managerDef.QueryType, managerDef.ResultType });

                var manager = System.ActorOf(Props.Create(generics),
                    $"query-{managerDef.QueryType.Name}-manager");

                dictionaryQuery.Add(managerDef, manager);
            }

            DefinitionToQueryManager = dictionaryQuery;

            return this;
        }

        public DefinitionToManagerRegistryBuilder RegisterSagaManagers(IReadOnlyCollection<ISagaManagerDefinition> definitions)
        {
            var dictionarySaga = new Dictionary<ISagaManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var type = typeof(AggregateSagaManager<,,>);
                var generics = type.MakeGenericType(new[] { managerDef.AggregateType, managerDef.IdentityType, managerDef.SagaLocatorType });

                var manager = System.ActorOf(
                    Props.Create(generics),
                    $"saga-{managerDef.IdentityType.Name}-manager"
                );
                dictionarySaga.Add(managerDef, manager);
            }
            DefinitionToSagaManager = dictionarySaga;
            return this;
        }

        public DefinitionToManagerRegistryBuilder RegisterReadModelManagers(IReadOnlyCollection<IReadModelManagerDefinition> definitions)
        {
            var dictionaryReadModel = new Dictionary<IReadModelManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var manager = System.ActorOf(
                    Props.Create(managerDef.ReadModelManagerType),
                    $"readmodel-{managerDef.ReadModelType}-manager"
                );

                dictionaryReadModel.Add(managerDef, manager);
            }
            DefinitionToReadModelManager = dictionaryReadModel;
            return this;
        }

        public DefinitionToManagerRegistry Build()
        {
            return new DefinitionToManagerRegistry(DefinitionToAggregateManager, DefinitionToQueryManager, DefinitionToSagaManager, DefinitionToReadModelManager);
        }
    }
}
