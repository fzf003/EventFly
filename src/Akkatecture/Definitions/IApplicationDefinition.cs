﻿// Decompiled with JetBrains decompiler
// Type: Akkatecture.Definitions.IApplicationDefinition
// Assembly: Akkatecture, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 61DF059E-E5F5-4992-B320-644C3E4F5C82
// Assembly location: C:\Users\naych\source\repos\!!!!!\netcoreapp2.2\Akkatecture.dll

using System.Collections.Generic;
using Akkatecture.Jobs;

namespace Akkatecture.Definitions
{

    public interface IApplicationDefinition
    {
        IReadOnlyCollection<IDomainDefinition> Domains { get; }

        IReadOnlyCollection<IQueryDefinition> Queries { get; }

        IReadOnlyCollection<IAggregateDefinition> Aggregates { get; }

        IReadOnlyCollection<IReadModelDefinition> ReadModels { get; }

        IReadOnlyCollection<ISagaDefinition> Sagas { get; }

        ICommandDefinitions Commands { get; }

        IEventDefinitions Events { get; }

        IJobDefinitions Jobs { get; }

        ISnapshotDefinitions Snapshots { get; }

        TDomainDefinition RegisterDomainDefenitions<TDomainDefinition>() where TDomainDefinition : IDomainDefinition, new();
    }
}
