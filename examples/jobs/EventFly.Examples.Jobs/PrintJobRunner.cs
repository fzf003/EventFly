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

using Akka.Event;
using EventFly.Infrastructure.Jobs;
using EventFly.Jobs;

namespace EventFly.Examples.Jobs
{
    public class PrintJobRunner : JobRunner<PrintJob, PrintJobId>,
        IRun<PrintJob, PrintJobId>
    {
        public System.Boolean Run(PrintJob job)
        {
            //Only thing that print runner does is it prints out the contents of the job message
            var time = Context.System.Scheduler.Now.DateTime;
            Context
                .GetLogger()
                .Info(
                    "PrinJobRunner at Timestamp={0}, is printing Content={1}",
                    time,
                    job.Content);

            return true;
        }
    }
}