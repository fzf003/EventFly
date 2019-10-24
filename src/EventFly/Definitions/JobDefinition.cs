﻿// Decompiled with JetBrains decompiler
// Type: EventFly.Definitions.JobDefinition
// Assembly: EventFly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 61DF059E-E5F5-4992-B320-644C3E4F5C82
// Assembly location: C:\Users\naych\source\repos\!!!!!\netcoreapp2.2\EventFly.dll

using System;
using EventFly.Extensions;
using EventFly.Jobs;

namespace EventFly.Definitions
{
    public interface IJobDefinition
    {
        JobName Name { get; }
        Type Type { get; }
        Type IdentityType { get; }
        IJobManagerDefinition ManagerDefinition { get; }
    }

    public interface IJobManagerDefinition
    {
        Type JobSchedulreType { get; }
        Type JobRunnerType { get; }
        Type JobType { get; }
        Type IdentityType { get; }
    }

    internal class JobDefinition : IJobDefinition
    {
        internal JobDefinition(Type type, Type identityType, IJobManagerDefinition managerDefinition)
        {
            Type = type;
            Name = type.GetJobName();
            IdentityType = identityType;
            ManagerDefinition = managerDefinition;
        }

        public JobName Name { get; }

        public Type Type { get; }

        public Type IdentityType { get; }

        public IJobManagerDefinition ManagerDefinition { get; }
    }

    internal sealed class JobManagerDefinition : IJobManagerDefinition
    {
        internal JobManagerDefinition(Type jobRunnerType, Type jobSchedulreType, Type jobType, Type identityType)
        {
            JobRunnerType = jobRunnerType;
            JobSchedulreType = jobSchedulreType;
            JobType = jobType;
            IdentityType = identityType;
        }

        public Type JobRunnerType { get; }
        public Type JobSchedulreType { get; }
        public Type JobType { get; }
        public Type IdentityType { get; }
    }
}
