﻿using EventFly.Aggregates;
using EventFly.Core;

namespace EventFly.Domain
{
    public interface IDomainServiceIsStartedByAsync<in TIdentity, in TAggregateEvent> : IDomainServiceHandlesAsync<TIdentity, TAggregateEvent>
        where TAggregateEvent : class, IAggregateEvent<TIdentity>
        where TIdentity : IIdentity
    { }
}