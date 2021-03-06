using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.DI.Core;
using EventFly.Aggregates;
using EventFly.Definitions;
using EventFly.DomainService;
using EventFly.Extensions;
using EventFly.Infrastructure.Jobs;
using EventFly.Infrastructure.Schedulers.Commands;
using EventFly.Infrastructure.Schedulers.Events;
using EventFly.Queries;
using EventFly.Sagas.AggregateSaga;
using EventFly.Schedulers.Commands;
using EventFly.Schedulers.Events;

namespace EventFly.Infrastructure.Definitions
{
    internal sealed class DefinitionToManagerRegistry : IDefinitionToManagerRegistry
    {
        public IReadOnlyDictionary<IAggregateManagerDefinition, IActorRef> DefinitionToAggregateManager { get; } = new Dictionary<IAggregateManagerDefinition, IActorRef>();
        public IReadOnlyDictionary<IQueryManagerDefinition, IActorRef> DefinitionToQueryManager { get; } = new Dictionary<IQueryManagerDefinition, IActorRef>();
        public IReadOnlyDictionary<ISagaManagerDefinition, IActorRef> DefinitionToSagaManager { get; } = new Dictionary<ISagaManagerDefinition, IActorRef>();
        public IReadOnlyDictionary<IReadModelManagerDefinition, IActorRef> DefinitionToReadModelManager { get; } = new Dictionary<IReadModelManagerDefinition, IActorRef>();
        public IReadOnlyDictionary<IJobManagerDefinition, IActorRef> DefinitionToJobManager { get; } = new Dictionary<IJobManagerDefinition, IActorRef>();
        public IReadOnlyDictionary<IDomainServiceManagerDefinition, IActorRef> DefinitionToDomainServiceManager { get; } = new Dictionary<IDomainServiceManagerDefinition, IActorRef>();

        public DefinitionToManagerRegistry(ActorSystem actorSystem, IApplicationDefinition applicationDefinition)
        {
            _system = actorSystem;

            foreach (var context in applicationDefinition.Contexts)
            {
                DefinitionToAggregateManager = DefinitionToAggregateManager.Union(RegisterAggregateManagers(context.Aggregates.Select(a => a.ManagerDefinition).ToList(), context.Name)).ToDictionary(k => k.Key, v => v.Value);
                DefinitionToQueryManager = DefinitionToQueryManager.Union(RegisterQueryManagers(context.Queries.Select(a => a.ManagerDefinition).ToList(), context.Name)).ToDictionary(k => k.Key, v => v.Value);
                DefinitionToReadModelManager = DefinitionToReadModelManager.Union(RegisterReadModelManagers(context.ReadModels.Select(a => a.ManagerDefinition).ToList(), context.Name)).ToDictionary(k => k.Key, v => v.Value);
                DefinitionToSagaManager = DefinitionToSagaManager.Union(RegisterSagaManagers(context.Sagas.Select(a => a.ManagerDefinition).ToList(), context.Name)).ToDictionary(k => k.Key, v => v.Value);
                DefinitionToDomainServiceManager = DefinitionToDomainServiceManager.Union(RegisterDomainServiceManagers(context.DomainServices.Select(a => a.ManagerDefinition).ToList(), context.Name)).ToDictionary(k => k.Key, v => v.Value);
                DefinitionToJobManager = DefinitionToJobManager
                    .Union(RegisterJobManagers(context.Jobs.Select(a => a.ManagerDefinition).ToList(), context.Name))
                    .ToDictionary(k => k.Key, v => v.Value);
            }

            DefinitionToJobManager = DefinitionToJobManager
                .Union(RegisterCommandsScheduler("common"))
                .Union(RegisterEventsScheduler("common"))
                .ToDictionary(k => k.Key, v => v.Value);

            RegisterDomainEventSubscribers(applicationDefinition.DomainEventSubscribers);
        }

        private IReadOnlyDictionary<IJobManagerDefinition, IActorRef> RegisterJobManagers(IReadOnlyCollection<IJobManagerDefinition> definitions, String contextName)
        {
            var dictionaryJob = new Dictionary<IJobManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var type = typeof(JobManager<,,,>);
                var generic = type.MakeGenericType(new[] { managerDef.JobSchedulreType, managerDef.JobRunnerType, managerDef.JobType, managerDef.IdentityType });
                var manager = _system.ActorOf(Props.Create(generic), $"job-{contextName}-{managerDef.IdentityType.Name}-manager");
                dictionaryJob.Add(managerDef, manager);
            }
            return dictionaryJob;
        }

        public IReadOnlyDictionary<IJobManagerDefinition, IActorRef> RegisterCommandsScheduler(String contextName)
        {
            var def = new JobManagerDefinition(typeof(PublishCommandJobRunner), typeof(PublishCommandJobScheduler), typeof(PublishCommandJob), typeof(PublishCommandJobId));

            return RegisterJobManagers(new[] { def }, contextName);
        }

        public IReadOnlyDictionary<IJobManagerDefinition, IActorRef> RegisterEventsScheduler(String contextName)
        {
            var def = new JobManagerDefinition(typeof(PublishEventJobRunner), typeof(PublishEventJobScheduler), typeof(PublishEventJob), typeof(PublishEventJobId));

            return RegisterJobManagers(new[] { def }, contextName);
        }

        private void RegisterDomainEventSubscribers(IReadOnlyCollection<IDomainEventSubscriberDefinition> domainEventSubscriberDefinitions)
        {
            foreach (var subscriberDefinition in domainEventSubscriberDefinitions)
            {
                Props subscriberProps;
                try
                {
                    subscriberProps = _system.DI().Props(subscriberDefinition.Type);
                }
                catch (Exception ex)
                {
                    _system.Log.Error(ex, "No DI available at the moment, falling back to default props creation.");
                    subscriberProps = Props.Create(subscriberDefinition.Type);
                }
                _system.ActorOf(subscriberProps);
            }
        }

        private readonly ActorSystem _system;

        private IReadOnlyDictionary<IAggregateManagerDefinition, IActorRef> RegisterAggregateManagers(IReadOnlyCollection<IAggregateManagerDefinition> definitions, String contextName)
        {
            var dictionaryAggregate = new Dictionary<IAggregateManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var type = typeof(AggregateManager<,>);
                var generics = type.MakeGenericType(new[] { managerDef.AggregateType, managerDef.IdentityType });
                var manager = _system.ActorOf(Props.Create(generics), $"aggregate-{contextName}-{managerDef.AggregateType.GetAggregateName()}-manager");
                dictionaryAggregate.Add(managerDef, manager);
            }
            return dictionaryAggregate;
        }

        private IReadOnlyDictionary<IQueryManagerDefinition, IActorRef> RegisterQueryManagers(IReadOnlyCollection<IQueryManagerDefinition> definitions, String contextName)
        {
            var dictionaryQuery = new Dictionary<IQueryManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var type = typeof(QueryManager<,,>);
                var generics = type.MakeGenericType(new[] { managerDef.QueryHandlerType, managerDef.QueryType, managerDef.ResultType });
                var manager = _system.ActorOf(Props.Create(generics), $"query-{contextName}-{managerDef.QueryType.Name}-manager");
                dictionaryQuery.Add(managerDef, manager);
            }
            return dictionaryQuery;
        }

        private IReadOnlyDictionary<ISagaManagerDefinition, IActorRef> RegisterSagaManagers(IReadOnlyCollection<ISagaManagerDefinition> definitions, String contextName)
        {
            var dictionarySaga = new Dictionary<ISagaManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var type = typeof(AggregateSagaManager<,,>);
                var generics = type.MakeGenericType(new[] { managerDef.AggregateType, managerDef.IdentityType, managerDef.SagaLocatorType });
                var manager = _system.ActorOf(Props.Create(generics), $"saga-{contextName}-{managerDef.IdentityType.Name}-manager");
                dictionarySaga.Add(managerDef, manager);
            }
            return dictionarySaga;
        }

        private IReadOnlyDictionary<IDomainServiceManagerDefinition, IActorRef> RegisterDomainServiceManagers(IReadOnlyCollection<IDomainServiceManagerDefinition> definitions, String contextName)
        {
            var dictionary = new Dictionary<IDomainServiceManagerDefinition, IActorRef>();
            foreach (var definition in definitions)
            {
                var type = typeof(DomainServiceManager<>).MakeGenericType(definition.ServiceType);
                var actor = _system.ActorOf(Props.Create(type), $"service-{contextName}-{definition.ServiceType.Name}-manager");
                dictionary.Add(definition, actor);
            }
            return dictionary;
        }

        private IReadOnlyDictionary<IReadModelManagerDefinition, IActorRef> RegisterReadModelManagers(IReadOnlyCollection<IReadModelManagerDefinition> definitions, String contextName)
        {
            var dictionaryReadModel = new Dictionary<IReadModelManagerDefinition, IActorRef>();
            foreach (var managerDef in definitions)
            {
                var manager = _system.ActorOf(Props.Create(managerDef.ReadModelManagerType), $"readmodel-{contextName}-{managerDef.ReadModelType}-manager");
                dictionaryReadModel.Add(managerDef, manager);
            }
            return dictionaryReadModel;
        }
    }
}