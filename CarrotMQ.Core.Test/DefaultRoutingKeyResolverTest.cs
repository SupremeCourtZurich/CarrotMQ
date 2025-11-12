using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.Test.RoutingKeyResolverTestNamespace;

namespace CarrotMQ.Core.Test
{
    [TestClass]
    public class DefaultRoutingKeyResolverTest
    {
        private const int MaxLength = 256;

        [TestMethod]
        public void RoutingKeyWithTooLongNestedClassNameTest()
        {
            var resolver = new DefaultRoutingKeyResolver();

            var routingKey = resolver
                .GetRoutingKey<
                    MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryTooLongClassName
                    .MyClassName>("exchangeX");

            Assert.AreEqual(MaxLength, routingKey.Length);
            Assert.AreEqual(
                "CarrotMQ.Core.Test.RoutingKeyResolverTestNamespace.MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryV...MyClassName",
                routingKey);
        }

        [TestMethod]
        public void RoutingKeyWithClassTest()
        {
            var resolver = new DefaultRoutingKeyResolver();

            var routingKey = resolver
                .GetRoutingKey<MyClassName>("exchangeX");

            Assert.AreEqual("CarrotMQ.Core.Test.RoutingKeyResolverTestNamespace.MyClassName", routingKey);
        }

        [TestMethod]
        public void RoutingKeyWithTooLongNamespaceClassTest()
        {
            var resolver = new DefaultRoutingKeyResolver();

            var routingKey = resolver
                .GetRoutingKey<RoutingKeyResolverTestNamespace.
                    MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongNamespace
                    .MyClassName>("exchangeX");

            Assert.AreEqual(MaxLength, routingKey.Length);
            Assert.AreEqual(
                "CarrotMQ.Core.Test.RoutingKeyResolverTestNamespace.MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryV...MyClassName",
                routingKey);
        }

        [TestMethod]
        public void RoutingKeyWithTooLongNamespaceNestedClassTest()
        {
            var resolver = new DefaultRoutingKeyResolver();

            var routingKey = resolver
                .GetRoutingKey<RoutingKeyResolverTestNamespace.
                    MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongNamespace
                    .MyClassName.MyInnerClassName>("exchangeX");

            Assert.AreEqual(MaxLength, routingKey.Length);
            Assert.AreEqual(
                "CarrotMQ.Core.Test.RoutingKeyResolverTestNamespace.MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVery...MyInnerClassName",
                routingKey);
        }
    }
}

namespace CarrotMQ.Core.Test.RoutingKeyResolverTestNamespace
{
    public class
#pragma warning disable MA0048 // File name must match type name
        MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryTooLongClassName
#pragma warning restore MA0048 // File name must match type name
    {
        public class MyClassName;
    }

#pragma warning disable MA0048 // File name must match type name
    public class MyClassName;
#pragma warning restore MA0048 // File name must match type name

    namespace
        MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongNamespace
    {
#pragma warning disable MA0048 // File name must match type name
        public class MyClassName
#pragma warning restore MA0048 // File name must match type name
        {
            public class MyInnerClassName;
        }
    }
}