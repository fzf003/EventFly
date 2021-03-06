using System;
using System.ComponentModel;
using EventFly.Tests.Data.Abstractions.Entities;
using FluentAssertions;
using Xunit;

namespace EventFly.Tests.Abstractions.Entities
{
    [Category(Categories.Abstractions)]
    [Collection(Collections.Only)]
    public class EntityTests
    {
        [Fact]
        public void InstantiatingEntity_WithNullId_ThrowsException()
        {
            this.Invoking(test => new Test(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void InstantiatingEntity_WithValidId_HasIdentity()
        {
            var testId = TestId.New;

            var test = new Test(testId);

            test.GetIdentity().Should().Be(testId);
        }
    }
}