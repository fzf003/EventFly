using Akka.TestKit.Xunit2;
using EventFly.Definitions;
using EventFly.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit.Abstractions;

namespace EventFly.TestFixture
{
    public abstract class EventFlyTestKit : TestKit
    {
        private const String _config =
            @"  akka.loglevel = ""INFO""
                akka.stdout-loglevel = ""INFO""
                akka.actor.serialize-messages = off
                loggers = [""Akka.TestKit.TestEventListener, Akka.TestKit""] 
                akka.persistence.snapshot-store {
                    plugin = ""akka.persistence.snapshot-store.inmem""
                    # List of snapshot stores to start automatically. Use "" for the default snapshot store.
                    auto-start-snapshot-stores = []
                }
                akka.persistence.snapshot-store.inmem {
                    # Class name of the plugin.
                    class = ""Akka.Persistence.Snapshot.MemorySnapshotStore, Akka.Persistence""
                    # Dispatcher for the plugin actor.
                    plugin-dispatcher = ""akka.actor.default-dispatcher""
                }
            ";

        protected EventFlyTestKit(ITestOutputHelper testOutputHelper) : base(_config, testOutputHelper) { }
    }

    public class EventFlyTestKit<T> : EventFlyTestKit
        where T : ContextDefinition, new()
    {
        public EventFlyTestKit(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            new ServiceCollection()
                .AddEventFly(Sys)
                .WithContext<T>()
                .Services
                .BuildServiceProvider()
                .UseEventFly();
        }
    }

    public class EventFlyTestKit<T1, T2> : EventFlyTestKit
        where T1 : ContextDefinition, new()
        where T2 : ContextDefinition, new()
    {
        public EventFlyTestKit(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            new ServiceCollection()
                .AddEventFly(Sys)
                .WithContext<T1>()
                .WithContext<T2>()
                .Services
                .BuildServiceProvider()
                .UseEventFly();
        }
    }

    public class EventFlyTestKit<T1, T2, T3> : EventFlyTestKit
        where T1 : ContextDefinition, new()
        where T2 : ContextDefinition, new()
        where T3 : ContextDefinition, new()
    {
        public EventFlyTestKit(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            new ServiceCollection()
                .AddEventFly(Sys)
                .WithContext<T1>()
                .WithContext<T2>()
                .WithContext<T3>()
                .Services
                .BuildServiceProvider()
                .UseEventFly();
        }
    }
}