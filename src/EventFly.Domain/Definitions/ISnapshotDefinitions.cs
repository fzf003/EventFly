﻿// Decompiled with JetBrains decompiler
// Type: EventFly.Definitions.ISnapshotDefinitions
// Assembly: EventFly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 61DF059E-E5F5-4992-B320-644C3E4F5C82
// Assembly location: C:\Users\naych\source\repos\!!!!!\netcoreapp2.2\EventFly.dll

using EventFly.Aggregates.Snapshot;
using EventFly.Core.VersionedTypes;

namespace EventFly.Definitions
{
    public interface ISnapshotDefinitions : IVersionedTypeDefinitions<SnapshotVersionAttribute, SnapshotDefinition>
    {
    }
}
