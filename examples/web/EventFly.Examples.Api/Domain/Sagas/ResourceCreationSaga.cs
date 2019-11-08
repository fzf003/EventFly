﻿// The MIT License (MIT)
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

using System;
using System.Threading.Tasks;
using EventFly.Aggregates;
using EventFly.Examples.Api.Domain.Aggregates.Resource;
using EventFly.Examples.Api.Domain.Aggregates.Resource.Events;
using EventFly.Examples.Api.Domain.Sagas.Events;
using EventFly.Sagas;
using EventFly.Sagas.AggregateSaga;

namespace EventFly.Examples.Api.Domain.Sagas
{
    public class ResourceCreationSaga : AggregateSaga<ResourceCreationSaga, ResourceCreationSagaId, ResourceCreationSagaState>,
        ISagaIsStartedBy<ResourceId, ResourceCreatedEvent>
    {
        public async Task Handle(IDomainEvent<ResourceId, ResourceCreatedEvent> domainEvent)
        {
            //simulates a long running process
            var resourceId = domainEvent.AggregateIdentity;
            var startedEvent = new ResourceCreationStartedEvent(resourceId, DateTime.UtcNow);
            var started = DateTimeOffset.UtcNow;
            Emit(startedEvent);
            
            var rng = new Random();
            var progress = 0;
            
            while (progress < 100)
            {
                var delay = rng.Next(1, 3);
                
                await Task.Delay(delay * 1000);
                progress += rng.Next(5, 15);
                var elapsed = DateTimeOffset.UtcNow - started;
                var progressEvent = new ResourceCreationProgressEvent(resourceId,progress,(int)elapsed.TotalSeconds,DateTime.UtcNow);
                Emit(progressEvent);
            }

            var elapsedTime = DateTimeOffset.UtcNow - started;
            var endedEvent = new ResourceCreationEndedEvent(resourceId,100, (int)elapsedTime.TotalSeconds,DateTime.UtcNow);
            
            Emit(endedEvent);
        }
    }
}