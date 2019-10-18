using Akka.Actor;
using EventFly.Definitions;
using Demo.Commands;
using Demo.Domain.Aggregates;
using Demo.Domain.QueryHandlers;
using Demo.Events;
using Demo.Queries;

namespace Demo.Domain
{
    public class UserDomain : DomainDefinition
    {
        public UserDomain()
        {
            RegisterAggregate<UserAggregate, UserId>();
            RegisterSaga<TestSaga, TestSagaId>();
            RegisterCommand<CreateUserCommand>();
            RegisterCommand<RenameUserCommand>();
            RegisterQuery<UsersQueryHandler,UsersQuery,UsersResult>();
            RegisterQuery<EventPostersQueryHandler,EventPostersQuery,EventPosters>();
            RegisterEvents(typeof(UserCreatedEvent), typeof(UserRenamedEvent));
        }
    }

}