// The MIT License (MIT)
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

using System.ComponentModel;
using System.Threading.Tasks;
using EventFly.Aggregates;
using EventFly.Commands;
using EventFly.DependencyInjection;
using EventFly.TestFixture;
using EventFly.Tests.Data.Abstractions;
using EventFly.Tests.Data.Abstractions.Commands;
using EventFly.Tests.Data.Abstractions.Entities;
using EventFly.Tests.Data.Abstractions.Events;
using EventFly.Tests.Data.Abstractions.Events.Signals;
using EventFly.Tests.Data.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EventFly.Tests.Domain.Aggregates
{
    [Category(Categories.Domain)]
    [Collection(Collections.Only)]
    public class AggregateTests : AggregateTestKit<TestContext>
    {
        public AggregateTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Fact]
        public void InitialState_AfterAggregateCreation_TestCreatedEventEmitted()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestCreatedEvent>));
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new CreateTestCommand(aggregateId, commandId);
            bus.Publish(command).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestCreatedEvent>>(
                x => x.AggregateEvent.TestAggregateId.Equals(aggregateId) &&
                x.Metadata.ContainsKey("some-key"));
        }

        [Fact]
        public async Task SendingCommand_ToAggregateRoot_ShouldReplyWithProperMessage()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestCreatedEvent>));
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new CreateTestCommand(aggregateId, commandId);
            var result = (ITestExecutionResult)await bus.Publish(command);

            result.IsSuccess.Should().BeTrue();
            result.SourceId.Should().Be(command.Metadata.SourceId);
        }

        [Fact]
        public void EventContainerMetadata_AfterAggregateCreation_TestCreatedEventEmitted()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestCreatedEvent>));
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new CreateTestCommand(aggregateId, commandId);
            bus.Publish(command).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestCreatedEvent>>(
                x => x.AggregateIdentity.Equals(aggregateId)
                && x.IdentityType == typeof(TestAggregateId)
                && x.EventType == typeof(TestCreatedEvent)
                && x.Metadata.EventName == "TestCreated"
                && x.Metadata.AggregateId == aggregateId.Value
                && x.Metadata.EventVersion == 1
                && x.Metadata.SourceId.Value == commandId.Value
                && x.Metadata.AggregateSequenceNumber == 1);
        }

        [Fact]
        public void InitialState_AfterAggregateCreation_TestStateSignalled()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestStateSignalEvent>));
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new CreateTestCommand(aggregateId, commandId);
            var nextCommand = new PublishTestStateCommand(aggregateId);
            bus.Publish(command).GetAwaiter().GetResult();
            bus.Publish(nextCommand).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestStateSignalEvent>>(
                x => x.AggregateEvent.LastSequenceNr == 1
                && x.AggregateEvent.Version == 1
                && x.AggregateEvent.AggregateState.TestCollection.Count == 0);
        }

        [Fact]
        public void TestCommand_AfterAggregateCreation_TestEventEmitted()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestAddedEvent>));
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new CreateTestCommand(aggregateId, commandId);
            var testId = TestId.New;
            var test = new Test(testId);
            var nextCommandId = CommandId.New;
            var nextCommand = new AddTestCommand(aggregateId, nextCommandId, test);
            bus.Publish(command).GetAwaiter().GetResult();
            bus.Publish(nextCommand).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>(x => x.AggregateEvent.Test.Equals(test));
        }

        [Fact]
        public void TestCommandTwice_AfterAggregateCreation_TestEventEmitted()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestAddedEvent>));
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new CreateTestCommand(aggregateId, commandId);
            var testId = TestId.New;
            var test = new Test(testId);
            var nextCommandId = CommandId.New;
            var nextCommand = new AddTestCommand(aggregateId, nextCommandId, test);
            var test2Id = TestId.New;
            var test2 = new Test(test2Id);
            var nextCommandId2 = CommandId.New;
            var nextCommand2 = new AddTestCommand(aggregateId, nextCommandId2, test2);
            bus.Publish(command).GetAwaiter().GetResult();
            bus.Publish(nextCommand).GetAwaiter().GetResult();
            bus.Publish(nextCommand2).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>(x => x.AggregateEvent.Test.Equals(test) && x.AggregateSequenceNumber == 2);
            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>(x => x.AggregateEvent.Test.Equals(test2) && x.AggregateSequenceNumber == 3);
        }

        [Fact]
        public void TestEventSourcing_AfterManyTests_TestStateSignalled()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestStateSignalEvent>));
            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var command = new CreateTestCommand(aggregateId, commandId);
            bus.Publish(command).GetAwaiter().GetResult();

            for (var i = 0; i < 5; i++)
            {
                var test = new Test(TestId.New);
                var testCommandId = CommandId.New;
                var testCommand = new AddTestCommand(aggregateId, testCommandId, test);
                bus.Publish(testCommand).GetAwaiter().GetResult();
            }

            var poisonCommand = new PoisonTestAggregateCommand(aggregateId);
            bus.Publish(poisonCommand).GetAwaiter().GetResult();

            var reviveCommand = new PublishTestStateCommand(aggregateId);
            bus.Publish(reviveCommand).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestStateSignalEvent>>(
                x => x.AggregateEvent.LastSequenceNr == 6
                && x.AggregateEvent.Version == 6
                && x.AggregateEvent.AggregateState.TestCollection.Count == 5);
        }

        [Fact]
        public void TestEventMultipleEmitSourcing_AfterManyMultiCreateCommand_EventsEmitted()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestCreatedEvent>));
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestAddedEvent>));
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var firstTest = new Test(TestId.New);
            var secondTest = new Test(TestId.New);
            var command = new CreateAndAddTwoTestsCommand(aggregateId, commandId, firstTest, secondTest);

            bus.Publish(command).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestCreatedEvent>>();
            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>();
            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>();
        }

        [Fact]
        public void TestEventMultipleEmitSourcing_AfterManyMultiCommand_TestStateSignalled()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestAddedEvent>));
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestStateSignalEvent>));
            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var command = new CreateTestCommand(aggregateId, commandId);
            bus.Publish(command).GetAwaiter().GetResult();

            var test = new Test(TestId.New);
            var testSourceId = CommandId.New;
            var testCommand = new AddFourTestsCommand(aggregateId, testSourceId, test);
            bus.Publish(testCommand).GetAwaiter().GetResult();

            var poisonCommand = new PoisonTestAggregateCommand(aggregateId);
            bus.Publish(poisonCommand).GetAwaiter().GetResult();

            var reviveCommand = new PublishTestStateCommand(aggregateId);
            bus.Publish(reviveCommand).GetAwaiter().GetResult();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>();
            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>();
            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>();
            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestAddedEvent>>();

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestStateSignalEvent>>(
                x => x.AggregateEvent.LastSequenceNr == 5
                && x.AggregateEvent.Version == 5
                && x.AggregateEvent.AggregateState.TestCollection.Count == 4);
        }

        [Fact]
        public void TestSnapShotting_AfterManyTests_TestStateSignalled()
        {
            var eventProbe = CreateTestProbe("event-probe");
            Sys.EventStream.Subscribe(eventProbe, typeof(IDomainEvent<TestAggregateId, TestStateSignalEvent>));
            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;

            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            var command = new CreateTestCommand(aggregateId, commandId);
            bus.Publish(command).GetAwaiter().GetResult();

            for (var i = 0; i < 10; i++)
            {
                var test = new Test(TestId.New);
                var testCommandId = CommandId.New;
                var testCommand = new AddTestCommand(aggregateId, testCommandId, test);
                bus.Publish(testCommand).GetAwaiter().GetResult();
            }

            eventProbe.ExpectMsg<IDomainEvent<TestAggregateId, TestStateSignalEvent>>(
                x => x.AggregateEvent.LastSequenceNr == 11
                && x.AggregateEvent.Version == 11
                && x.AggregateEvent.AggregateState.TestCollection.Count == 10
                && x.AggregateEvent.AggregateState.FromHydration);
        }

        [Fact]
        public async Task InitialState_TestingSuccessCommand_SuccessResultReplied()
        {
            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new TestSuccessExecutionResultCommand(aggregateId, commandId);
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            await bus.Publish(command);
        }

        [Fact]
        public async Task InitialState_TestingFailedCommand_SuccessResultReplied()
        {
            var aggregateId = TestAggregateId.New;
            var commandId = CommandId.New;
            var command = new TestFailedExecutionResultCommand(aggregateId, commandId);
            var bus = Sys.GetExtension<ServiceProviderHolder>().ServiceProvider.GetRequiredService<ICommandBus>();

            await bus.Publish(command);
        }

        [Fact]
        public void TestDistinctCommand_AfterTwoHandles_CommandFails()
        {
            // TODO https://dev.azure.com/lutando/EventFly/_workitems/edit/25
            /* 
             var probe = CreateTestProbe("event-probe");
             var aggregateManager = Sys.ActorOf(Props.Create(() => new TestAggregateManager()), "test-aggregatemanager");
             var aggregateId = TestAggregateId.New;
             var createCommand = new CreateTestCommand(aggregateId);
             aggregateManager.Tell(createCommand);

             var command = new TestDistinctCommand(aggregateId, 10);

             aggregateManager.Tell(command, probe);

             probe.ExpectNoMsg();

             aggregateManager.Tell(command, probe);

             probe.ExpectMsg<FailedExecutionResult>(TimeSpan.FromHours(1));
             */
        }
    }
}