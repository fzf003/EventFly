﻿// Decompiled with JetBrains decompiler
// Type: Akkatecture.Definitions.EventAggregatedDefinitions
// Assembly: Akkatecture, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 61DF059E-E5F5-4992-B320-644C3E4F5C82
// Assembly location: C:\Users\naych\source\repos\!!!!!\netcoreapp2.2\Akkatecture.dll

using Akkatecture.Aggregates;

namespace Akkatecture.Definitions
{
  public class EventAggregatedDefinitions : AggregatedDefinitions<EventDefinitions, EventVersionAttribute, EventDefinition>, IEventDefinitions
  {
  }
}
