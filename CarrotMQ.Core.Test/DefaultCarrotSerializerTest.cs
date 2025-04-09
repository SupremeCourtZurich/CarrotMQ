using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;

namespace CarrotMQ.Core.Test;

[TestClass]
public class DefaultCarrotSerializerTest
{
    private DefaultCarrotSerializer _serializer = null!;

    [TestInitialize]
    public void Setup()
    {
        _serializer = new DefaultCarrotSerializer();
    }

    [TestMethod]
    public void SerializePropertiesOfDerivedClasses()
    {
        IEvent<TestEvent, TestExchangeEndPoint> testEvent = new TestEvent();

        var serialized = _serializer.Serialize(testEvent);

        Assert.AreEqual("{\"TestProp\":\"prop\"}", serialized);
    }

    [TestMethod]
    [DataTestMethod]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(double.NaN)]
    public void DoubleNotFiniteNumberSerializationTest(double numberUnderTest)
    {
        var serialized = _serializer.Serialize(numberUnderTest);
        double? deserialized = _serializer.Deserialize<double>(serialized);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(numberUnderTest, deserialized.Value);
    }
}

public class TestEvent : IEvent<TestEvent, TestExchangeEndPoint>
{
    public string TestProp { get; set; } = "prop";
}