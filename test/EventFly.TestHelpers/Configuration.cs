using Akka.Configuration;
using System;

namespace EventFly.Tests
{
    public static class Configuration
    {
        public static String Default =
            @"  akka.loglevel = ""INFO""
                akka.stdout-loglevel = ""INFO""
                akka.actor.serialize-messages = on
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

        public static Config WithTestScheduler =
            ConfigurationFactory.ParseString(
                @"EventFly.job-scheduler.tick-interval = 500ms
                akka.scheduler.implementation = ""Akka.TestKit.TestScheduler, Akka.TestKit""")
            .WithFallback(ConfigurationFactory.ParseString(Default));
    }
}