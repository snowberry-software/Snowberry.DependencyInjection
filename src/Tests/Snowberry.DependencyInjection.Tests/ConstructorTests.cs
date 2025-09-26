using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Xunit;

namespace Snowberry.DependencyInjection.Tests;

public class ConstructorTests
{
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
    private class ExpectedConstructorAttribute : Attribute { }

    [Theory]
    [InlineData(typeof(TestInstance0))]
    [InlineData(typeof(TestInstance1))]
    [InlineData(typeof(TestInstance2))]
    [InlineData(typeof(TestInstance3))]
    [InlineData(typeof(TestInstance4))]
    public void TestInstance(Type instanceType)
    {
        using var serviceContainer = new ServiceContainer();
        var constructor = serviceContainer.GetConstructor(instanceType);

        Assert.NotNull(constructor);
        Assert.NotNull(constructor!.GetCustomAttribute<ExpectedConstructorAttribute>());
    }

    private class TestInstance0
    {
        [ExpectedConstructor]
        public TestInstance0()
        {
        }
    }

    private class TestInstance1
    {
        [ExpectedConstructor]
        public TestInstance1(bool value0)
        {
        }
    }

    private class TestInstance2
    {
        [ExpectedConstructor]
        public TestInstance2(bool value0, int value1, long value2)
        {
        }
    }

    private class TestInstance3 : TestInstance2
    {
        [ExpectedConstructor]
        [PreferredConstructor]
        public TestInstance3(bool value0, int value1, long value2, float value3, double value4) : base(value0, value1, value2)
        {
        }
    }

    private class TestInstance4 : TestInstance3
    {
        [ExpectedConstructor]
        [PreferredConstructor]
        public TestInstance4(bool value0, int value1, long value2) : base(value0, value1, value2, 0, 0)
        {
        }

        public TestInstance4(bool value0, int value1, long value2, float value3, double value4, short value5) : base(value0, value1, value2, value3, value4)
        {
        }
    }
}
