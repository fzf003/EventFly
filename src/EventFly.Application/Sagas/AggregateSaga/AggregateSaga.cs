// The MIT License (MIT)
//
// Copyright (c) 2015-2019 Rasmus Mikkelsen
// Copyright (c) 2015-2019 eBay Software Foundation
// Modified from original source https://github.com/eventflow/EventFlow
//
// Copyright (c) 2018 - 2019 Lutando Ngqakaza
// https://github.com/Lutando/EventFly
//
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Akka.Event;
using Akka.Persistence;
using EventFly.Aggregates;
using EventFly.Aggregates.Snapshot;
using EventFly.Aggregates.Snapshot.Strategies;
using EventFly.Commands;
using EventFly.Commands.ExecutionResults;
using EventFly.Core;
using EventFly.Definitions;
using EventFly.DependencyInjection;
using EventFly.Extensions;
using EventFly.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SnapshotMetadata = EventFly.Aggregates.Snapshot.SnapshotMetadata;

namespace EventFly.Sagas.AggregateSaga
{
    public abstract class AggregateSaga<TAggregateSaga, TIdentity, TSagaState> : ReceivePersistentActor, IAggregateSaga<TIdentity>
        where TAggregateSaga : AggregateSaga<TAggregateSaga, TIdentity, TSagaState>
        where TIdentity : IIdentity
        where TSagaState : SagaState<TAggregateSaga, TIdentity>
    {
        private static IReadOnlyDictionary<Type, Action<TSagaState, IAggregateEvent>> ApplyMethodsFromState = typeof(TSagaState).GetAggregateSagaEventApplyMethods<TAggregateSaga, TIdentity, TSagaState>();
        private static readonly IReadOnlyDictionary<Type, Action<TSagaState, IAggregateSnapshot>> HydrateMethodsFromState = typeof(TSagaState).GetAggregateSnapshotHydrateMethods<TAggregateSaga, TIdentity, TSagaState>();
        private static readonly IAggregateName SagaName = typeof(TAggregateSaga).GetSagaName();
        private CircularBuffer<ISourceId> _previousSourceIds = new CircularBuffer<ISourceId>(100);

        protected IEventDefinitions _eventDefinitionService;
        protected ISnapshotDefinitions _snapshotDefinitionService;
        protected ISnapshotStrategy SnapshotStrategy { get; set; } = SnapshotNeverStrategy.Instance;
        public TSagaState State { get; }

        protected override void PreStart()
        {
            base.PreStart();

            _scope = _serviceProvider.CreateScope();
        }

        protected override void PostStop()
        {
            base.PostStop();
            _scope.Dispose();
        }

        public async Task<IExecutionResult> PublishCommandAsync<TCommandIdentity>(ICommand<TCommandIdentity> command) where TCommandIdentity : IIdentity
        {
            if (PinnedEvent != null)
            {
                command.Metadata.Merge(PinnedEvent.Metadata);
            }

            if (!command.Metadata.CorrelationIds.Contains(Id.Value))
            {
                command.Metadata.CorrelationIds = new List<String>(command.Metadata.CorrelationIds) { Id.Value };
            }

            var bus = _scope.ServiceProvider.GetRequiredService<ICommandBus>();

            return await bus.Publish(command);
        }

        public IAggregateName Name => SagaName;
        public override String PersistenceId { get; }
        public TIdentity Id { get; }
        public Int64 Version { get; protected set; }
        public Boolean IsNew => Version <= 0;
        public override Recovery Recovery => new Recovery(SnapshotSelectionCriteria.Latest);

        private readonly IServiceProvider _serviceProvider;

        private IServiceScope _scope;

        public AggregateSagaSettings Settings { get; }

        protected IDomainEvent PinnedEvent;

        protected AggregateSaga()
        {
            _serviceProvider = Context.System.GetExtension<ServiceProviderHolder>().ServiceProvider;

            Settings = new AggregateSagaSettings(Context.System.Settings.Config);
            var idValue = Context.Self.Path.Name;
            PersistenceId = idValue;
            Id = (TIdentity)Activator.CreateInstance(typeof(TIdentity), idValue);

            if (Id == null)
            {
                throw new InvalidOperationException(
                    $"Identity for Saga '{Id?.GetType().PrettyPrint()}' could not be activated.");
            }

            if ((this as TAggregateSaga) == null)
            {
                throw new InvalidOperationException(
                    $"AggregateSaga {Name} specifies Type={typeof(TAggregateSaga).PrettyPrint()} as generic argument, it should be its own type.");
            }

            if (State == null)
            {
                try
                {
                    State = (TSagaState)Activator.CreateInstance(typeof(TSagaState));
                }
                catch (Exception exception)
                {
                    Context.GetLogger().Error(exception, "AggregateSaga of Name={1}; was unable to activate SagaState of Type={0}.", Name, typeof(TSagaState).PrettyPrint());
                }

            }

            if (Settings.AutoReceive)
            {
                InitAsyncReceives();
            }


            if (Settings.UseDefaultEventRecover)
            {
                Recover<ICommittedEvent<TIdentity, IAggregateEvent<TIdentity>>>(Recover);
                Recover<RecoveryCompleted>(Recover);
            }


            if (Settings.UseDefaultSnapshotRecover)
                Recover<SnapshotOffer>(Recover);


            Command<SaveSnapshotSuccess>(SnapshotStatus);
            Command<SaveSnapshotFailure>(SnapshotStatus);

            var appDefe = _serviceProvider.GetRequiredService<IApplicationDefinition>();
            _eventDefinitionService = appDefe.Events;
            _snapshotDefinitionService = appDefe.Snapshots;

        }


        public void InitAsyncReceives()
        {
            var type = GetType();

            var subscriptionTypes =
                type
                    .GetSagaEventSubscriptionTypes();

            var methods = type
                .GetTypeInfo()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "Handle")
                        return false;

                    var parameters = mi.GetParameters();

                    return
                        parameters.Length == 1;
                })
                .ToDictionary(
                    mi => mi.GetParameters()[0].ParameterType,
                    mi => mi);


            var method = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "CommandInternal") return false;
                    var parameters = mi.GetParameters();
                    return
                        parameters.Length == 2
                        && parameters[0].ParameterType.Name.Contains("Func");
                })
                .First();

            foreach (var subscriptionType in subscriptionTypes)
            {
                var funcType = typeof(Func<,>).MakeGenericType(subscriptionType, typeof(Task));
                var subscriptionFunction = Delegate.CreateDelegate(funcType, this, methods[subscriptionType]);
                var actorReceiveMethod = method.MakeGenericMethod(subscriptionType);

                actorReceiveMethod.Invoke(this, new[] { subscriptionFunction, (Object)null });
            }
        }
        protected void CommandInternal<T>(Func<T, Task> handler, Object item)
        {
            CommandAsync<T>(e =>
            {
                if (e is IDomainEvent @event)
                {
                    PinnedEvent = @event;
                }
                return handler(e);
            });
        }

        protected virtual void Emit<TAggregateEvent>(TAggregateEvent aggregateEvent, IEventMetadata metadata = null)
            where TAggregateEvent : class, IAggregateEvent<TIdentity>
        {
            var committedEvent = From(aggregateEvent, Version, metadata);
            Persist(committedEvent, ApplyCommittedEvent);
        }

        public virtual void EmitAll(params IAggregateEvent<TIdentity>[] aggregateEvents)
        {
            var version = Version;

            var committedEvents = new List<Object>();
            foreach (var aggregateEvent in aggregateEvents)
            {
                var committedEvent = FromObject(aggregateEvent, version + 1);
                committedEvents.Add(committedEvent);
                version++;
            }

            PersistAll(committedEvents, ApplyObjectCommittedEvent);
        }

        protected virtual Object FromObject(Object aggregateEvent, Int64 version, IEventMetadata metadata = null)
        {
            if (aggregateEvent is IAggregateEvent)
            {
                var eventDefinition = _eventDefinitionService.GetDefinition(aggregateEvent.GetType());
                var aggregateSequenceNumber = version + 1;
                var eventId = EventId.NewDeterministic(
                    GuidFactories.Deterministic.Namespaces.Events,
                    $"{Id.Value}-v{aggregateSequenceNumber}");
                var now = DateTimeOffset.UtcNow;
                var eventMetadata = new EventMetadata(PinnedEvent.Metadata)
                {
                    Timestamp = now,
                    AggregateSequenceNumber = aggregateSequenceNumber,
                    AggregateName = Name.Value,
                    AggregateId = Id.Value,
                    EventId = eventId,
                    EventName = eventDefinition.Name,
                    EventVersion = eventDefinition.Version
                };

                eventMetadata.AddOrUpdateValue(MetadataKeys.TimestampEpoch, now.ToUnixTime().ToString());
                if (metadata != null)
                {
                    eventMetadata.AddRange(metadata);
                }
                var genericType = typeof(CommittedEvent<,>)
                    .MakeGenericType(typeof(TIdentity), aggregateEvent.GetType());


                var committedEvent = Activator.CreateInstance(
                    genericType,
                    Id,
                    aggregateEvent,
                    eventMetadata,
                    now,
                    aggregateSequenceNumber);

                return committedEvent;
            }

            throw new InvalidOperationException("could not perform the required mapping for committed event.");

        }

        private void ApplyObjectCommittedEvent(Object committedEvent)
        {
            try
            {
                var method = GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.IsFamily || m.IsPublic)
                    .Single(m => m.Name.Equals("ApplyCommittedEvent"));

                var genericMethod = method.MakeGenericMethod(committedEvent.GetType().GenericTypeArguments[2]);

                genericMethod.Invoke(this, new[] { committedEvent });
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Aggregate of Name={0}, and Id={1}; tried to invoke Method={2} with object Type={3} .", Name, Id, nameof(ApplyCommittedEvent), committedEvent.GetType().PrettyPrint());
            }
        }

        public virtual CommittedEvent<TIdentity, TAggregateEvent> From<TAggregateEvent>(TAggregateEvent aggregateEvent,
            Int64 version, IEventMetadata metadata = null)
            where TAggregateEvent : class, IAggregateEvent<TIdentity>
        {
            if (aggregateEvent == null)
            {
                throw new ArgumentNullException(nameof(aggregateEvent));
            }
            var eventDefinition = _eventDefinitionService.GetDefinition(aggregateEvent.GetType());
            var aggregateSequenceNumber = version + 1;
            var eventId = EventId.NewDeterministic(
                GuidFactories.Deterministic.Namespaces.Events,
                $"{Id.Value}-v{aggregateSequenceNumber}");
            var now = DateTimeOffset.UtcNow;
            var eventMetadata = new EventMetadata
            {
                Timestamp = now,
                AggregateSequenceNumber = aggregateSequenceNumber,
                AggregateName = Name.Value,
                AggregateId = Id.Value,
                EventId = eventId,
                EventName = eventDefinition.Name,
                EventVersion = eventDefinition.Version
            };
            eventMetadata.AddOrUpdateValue(MetadataKeys.TimestampEpoch, now.ToUnixTime().ToString());
            if (metadata != null)
            {
                eventMetadata.AddRange(metadata);
            }

            var committedEvent = new CommittedEvent<TIdentity, TAggregateEvent>(Id, aggregateEvent, eventMetadata, now, aggregateSequenceNumber);
            return committedEvent;
        }


        protected virtual IAggregateSnapshot<TAggregateSaga, TIdentity> CreateSnapshot()
        {
            Log.Info("AggregateSaga of Name={0}, and Id={2}; attempted to create a snapshot, override the {2}() method to get snapshotting to function.", Name, Id, nameof(CreateSnapshot));
            return null;
        }

        protected void ApplyCommittedEvent<TAggregateEvent>(ICommittedEvent<TIdentity, TAggregateEvent> committedEvent)
            where TAggregateEvent : class, IAggregateEvent<TIdentity>
        {
            var applyMethods = GetEventApplyMethods(committedEvent.AggregateEvent);
            applyMethods(committedEvent.AggregateEvent);

            Log.Info("AggregateSaga of Name={0}, and Id={1}; committed and applied an AggregateEvent of Type={2}", Name, Id, typeof(TAggregateEvent).PrettyPrint());

            Version++;

            var domainEvent = new DomainEvent<TIdentity, TAggregateEvent>(Id, committedEvent.AggregateEvent, committedEvent.EventMetadata, committedEvent.Timestamp, Version);

            Publish(domainEvent);

            if (SnapshotStrategy.ShouldCreateSnapshot(this))
            {
                var aggregateSnapshot = CreateSnapshot();
                if (aggregateSnapshot != null)
                {
                    var snapshotDefinition = _snapshotDefinitionService.GetDefinition(aggregateSnapshot.GetType());
                    var snapshotMetadata = new SnapshotMetadata
                    {
                        AggregateId = Id.Value,
                        AggregateName = Name.Value,
                        AggregateSequenceNumber = Version,
                        SnapshotName = snapshotDefinition.Name,
                        SnapshotVersion = snapshotDefinition.Version
                    };

                    var committedSnapshot =
                        new CommittedSnapshot<TAggregateSaga, TIdentity, IAggregateSnapshot<TAggregateSaga, TIdentity>>(
                            Id,
                            aggregateSnapshot,
                            snapshotMetadata,
                            committedEvent.Timestamp, Version);

                    SaveSnapshot(committedSnapshot);
                }
            }

        }


        protected virtual void Publish<TEvent>(TEvent aggregateEvent)
        {
            Context.System.EventStream.Publish(aggregateEvent);
            Log.Info("Aggregate of Name={0}, and Id={1}; published DomainEvent of Type={2}.", Name, Id, typeof(TEvent).PrettyPrint());
        }

        protected Action<IAggregateEvent> GetEventApplyMethods<TAggregateEvent>(TAggregateEvent aggregateEvent)
            where TAggregateEvent : IAggregateEvent<TIdentity>
        {
            var eventType = aggregateEvent.GetType();

            if (!ApplyMethodsFromState.TryGetValue(eventType, out Action<TSagaState, IAggregateEvent> applyMethod))
                throw new NotImplementedException($"SagaState of Type={State.GetType().PrettyPrint()} does not have an 'Apply' method that takes in an aggregate event of Type={eventType.PrettyPrint()} as an argument.");

            var aggregateApplyMethod = applyMethod.Bind(State);

            return aggregateApplyMethod;
        }

        protected virtual void ApplyEvent(IAggregateEvent<TIdentity> aggregateEvent)
        {
            var eventApplier = GetEventApplyMethods(aggregateEvent);

            eventApplier(aggregateEvent);

            Version++;
        }

        protected virtual Boolean Recover(ICommittedEvent<TIdentity, IAggregateEvent<TIdentity>> committedEvent)
        {
            try
            {
                Log.Debug("AggregateSaga of Name={0}, Id={1}, and Version={2}, is recovering with CommittedEvent of Type={3}.", Name, Id, Version, committedEvent.GetType().PrettyPrint());
                ApplyEvent(committedEvent.AggregateEvent);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Aggregate of Name={0}, Id={1}; while recovering with event of Type={2} caused an exception.", Name, Id, committedEvent.GetType().PrettyPrint());
                return false;
            }

            return true;
        }

        protected virtual Boolean Recover(SnapshotOffer aggregateSnapshotOffer)
        {
            try
            {
                Log.Debug("AggregateSaga of Name={0}, and Id={1}; has received a SnapshotOffer of Type={2}.", Name, Id, aggregateSnapshotOffer.Snapshot.GetType().PrettyPrint());
                var comittedSnapshot = aggregateSnapshotOffer.Snapshot as CommittedSnapshot<TAggregateSaga, TIdentity, IAggregateSnapshot<TAggregateSaga, TIdentity>>;
                if (comittedSnapshot != null) HydrateSnapshot(comittedSnapshot.AggregateSnapshot, aggregateSnapshotOffer.Metadata.SequenceNr);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "AggregateSaga of Name={0}, Id={1}; recovering with snapshot of Type={2} caused an exception.", Name, Id, aggregateSnapshotOffer.Snapshot.GetType().PrettyPrint());

                return false;
            }

            return true;
        }

        protected virtual void HydrateSnapshot(IAggregateSnapshot<TAggregateSaga, TIdentity> aggregateSnapshot, Int64 version)
        {
            var snapshotHydrater = GetSnapshotHydrateMethods(aggregateSnapshot);

            snapshotHydrater(aggregateSnapshot);

            Version = version;
        }

        protected Action<IAggregateSnapshot> GetSnapshotHydrateMethods<TAggregateSnapshot>(TAggregateSnapshot aggregateEvent)
            where TAggregateSnapshot : IAggregateSnapshot<TAggregateSaga, TIdentity>
        {
            var snapshotType = aggregateEvent.GetType();

            Action<TSagaState, IAggregateSnapshot> hydrateMethod;
            if (!HydrateMethodsFromState.TryGetValue(snapshotType, out hydrateMethod))
                throw new NotImplementedException($"SagaState of Type={State.GetType().PrettyPrint()} does not have a 'Hydrate' method that takes in an aggregate snapshot of Type={snapshotType.PrettyPrint()} as an argument.");

            var snapshotHydrateMethod = hydrateMethod.Bind(State);

            return snapshotHydrateMethod;
        }

        protected void SetSourceIdHistory(Int32 count)
        {
            _previousSourceIds = new CircularBuffer<ISourceId>(count);
        }

        public Boolean HasSourceId(ISourceId sourceId)
        {
            return !sourceId.IsNone() && _previousSourceIds.Any(s => s.Value == sourceId.Value);
        }

        public IIdentity GetIdentity()
        {
            return Id;
        }

        protected virtual void SetSnapshotStrategy(ISnapshotStrategy snapshotStrategy)
        {
            if (snapshotStrategy != null)
            {
                SnapshotStrategy = snapshotStrategy;
            }
        }
        protected virtual Boolean SnapshotStatus(SaveSnapshotSuccess snapshotSuccess)
        {
            Log.Debug("Aggregate of Name={0}, and Id={1}; saved a snapshot at Version={2}.", Name, Id, snapshotSuccess.Metadata.SequenceNr);
            DeleteSnapshots(new SnapshotSelectionCriteria(snapshotSuccess.Metadata.SequenceNr - 1));
            return true;
        }

        protected virtual Boolean SnapshotStatus(SaveSnapshotFailure snapshotFailure)
        {
            Log.Error(snapshotFailure.Cause, "Aggregate of Name={0}, and Id={1}; failed to save snapshot at Version={2}.", Name, Id, snapshotFailure.Metadata.SequenceNr);
            return true;
        }


        protected virtual Boolean Recover(RecoveryCompleted recoveryCompleted)
        {
            Log.Debug("Aggregate of Name={0}, and Id={1}; has completed recovering from it's event journal at Version={2}.", Name, Id, Version);
            return true;
        }
    }


    public class StatelessSaga<TAggregateSaga, TIdentity> : AggregateSaga<TAggregateSaga, TIdentity, EmptySagaState<TAggregateSaga, TIdentity>>
        where TAggregateSaga : AggregateSaga<TAggregateSaga, TIdentity, EmptySagaState<TAggregateSaga, TIdentity>>
        where TIdentity : IIdentity
    {

    }

    public class EmptySagaState<TAggregateSaga, TIdentity> : SagaState<TAggregateSaga, TIdentity>
        where TAggregateSaga : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
    {
    }
}