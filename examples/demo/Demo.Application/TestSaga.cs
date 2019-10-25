﻿using System;
using System.Threading.Tasks;
using EventFly.Aggregates;
using EventFly.Core;
using Demo.Commands;
using Demo.Events;
using Demo.ValueObjects;
using EventFly.Sagas;
using EventFly.Sagas.AggregateSaga;

namespace Demo.Application
{
    #region TestSagaId
    public class TestSagaId : Identity<TestSagaId> { public TestSagaId(string value) : base(value){} }
    
    #endregion
    
    public class TestSaga : StatelessSaga<TestSaga, TestSagaId>,
        ISagaIsStartedByAsync<UserId, UserCreatedEvent>,
        ISagaHandles<UserId,UserRenamedEvent>
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly string _s;

        public TestSaga(string s)
        {
            _s = s;
        }

        public async Task HandleAsync(IDomainEvent<UserId, UserCreatedEvent> domainEvent)
        {
            await PublishCommandAsync(new RenameUserCommand(domainEvent.AggregateIdentity, new UserName(DateTime.Now.ToLongDateString())));
        }

        public bool Handle(IDomainEvent<UserId, UserRenamedEvent> domainEvent)
        {
            Console.WriteLine($"FROM SAGA:Renamed to {domainEvent.AggregateEvent.NewName}");
            return true;
        }
    }
}
