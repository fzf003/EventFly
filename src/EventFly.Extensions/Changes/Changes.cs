﻿using EventFly.ValueObjects;

namespace EventFly.Extensions.Changes
{
    public sealed class Changes<T> where T : ValueObject
    {
        public T Value { get;}

        public Changes(T value)
        {
            Value = value;
        }
    }
}
