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

using Akka.Actor;
using Akka.Event;
using EventFly.Aggregates;
using EventFly.Extensions;
using EventFly.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EventFly.Subscribers
{
    public abstract class DomainEventSubscriber : ReceiveActor
    {
        private IReadOnlyList<Type> SubscriptionTypes { get; }
        protected ILoggingAdapter Logger { get; }
        public DomainEventSubscriberSettings Settings { get; }

        protected DomainEventSubscriber(DomainEventSubscriberSettings settings = null)
        {
            Logger = Context.GetLogger();
            if (settings == null) Settings = new DomainEventSubscriberSettings(Context.System.Settings.Config);
            else Settings = settings;

            SubscriptionTypes = new List<Type>();
            if (Settings.AutoSubscribe)
            {
                var type = GetType();

                var subscriptionTypes = new List<Type>();

                var domainEventsubscriptionTypes = type.GetDomainEventSubscriberSubscriptionTypes();
                subscriptionTypes.AddRange(domainEventsubscriptionTypes);

                var asyncDomainEventSubscriptionTypes = type.GetAsyncDomainEventSubscriberSubscriptionTypes();
                subscriptionTypes.AddRange(asyncDomainEventSubscriptionTypes);

                if (type.IsAsyncManyDomainEventSubscriber())
                {
                    var eventTypes = ((ISubscribeToManyAsync)this).GetEventTypes();
                    var domainEventTypes = eventTypes.AggregateEventTypeToDomainEventType();
                    subscriptionTypes.AddRange(domainEventTypes);
                }

                SubscriptionTypes = subscriptionTypes;

                foreach (var subscriptionType in SubscriptionTypes)
                {
                    Context.System.EventStream.Subscribe(Self, subscriptionType);
                }
            }

            if (Settings.AutoReceive)
            {
                InitReceives();
                InitAsyncReceives();
                InitAsyncManyReceiver();
            }

            Receive<UnsubscribeFromAll>(Handle);
        }

        protected void InitReceives()
        {
            var type = GetType();
            var subscriptionTypes = type.GetDomainEventSubscriberSubscriptionTypes();

            var methods = type
                .GetTypeInfo()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "Handle") return false;
                    var parameters = mi.GetParameters();
                    return parameters.Length == 1;
                })
                .ToDictionary(mi => mi.GetParameters()[0].ParameterType, mi => mi);

            var method = type
                .GetBaseType("ReceiveActor")
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "Receive") return false;
                    var parameters = mi.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType.Name.Contains("Func");
                })
                .First();

            foreach (var subscriptionType in subscriptionTypes)
            {
                var funcType = typeof(Func<,>).MakeGenericType(subscriptionType, typeof(Boolean));
                var subscriptionFunction = Delegate.CreateDelegate(funcType, this, methods[subscriptionType]);
                var actorReceiveMethod = method.MakeGenericMethod(subscriptionType);

                actorReceiveMethod.Invoke(this, new Object[] { subscriptionFunction });
            }
        }

        protected void InitAsyncReceives()
        {
            var type = GetType();
            var subscriptionTypes = type.GetAsyncDomainEventSubscriberSubscriptionTypes();

            var methods = type
                .GetTypeInfo()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "HandleAsync") return false;
                    var parameters = mi.GetParameters();
                    return parameters.Length == 1;
                })
                .ToDictionary(mi => mi.GetParameters()[0].ParameterType, mi => mi);

            var method = type
                .GetBaseType("ReceiveActor")
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "ReceiveAsync") return false;
                    var parameters = mi.GetParameters();
                    return parameters.Length == 2 && parameters[0].ParameterType.Name.Contains("Func");
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

        protected void InitAsyncManyReceiver()
        {
            var type = GetType();
            if (type.IsAsyncManyDomainEventSubscriber() == false)
                return;

            var handleMethod = type
                .GetTypeInfo()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "HandleAsync") return false;
                    var parameters = mi.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(IDomainEvent);
                })
                .First();

            var receiveMethod = type
                .GetBaseType("ReceiveActor")
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi =>
                {
                    if (mi.Name != "ReceiveAsync") return false;
                    var parameters = mi.GetParameters();
                    return parameters.Length == 2 && parameters[0].ParameterType.Name.Contains("Func");
                })
                .First();

            var funcType = typeof(Func<,>).MakeGenericType(typeof(IDomainEvent), typeof(Task));
            var subscriptionFunction = Delegate.CreateDelegate(funcType, this, handleMethod);
            var actorReceiveMethod = receiveMethod.MakeGenericMethod(typeof(IDomainEvent));

            actorReceiveMethod.Invoke(this, new[] { subscriptionFunction, (Object)null });
        }

        protected virtual Boolean Handle(UnsubscribeFromAll command)
        {
            UnsubscribeFromAllTopics();
            return true;
        }

        protected void UnsubscribeFromAllTopics()
        {
            foreach (var type in SubscriptionTypes)
            {
                Context.System.EventStream.Unsubscribe(Self, type);
            }
        }
    }
}