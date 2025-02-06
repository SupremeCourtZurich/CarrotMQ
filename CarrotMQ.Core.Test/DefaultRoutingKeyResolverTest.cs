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
        MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryTooLongClassName
    {
        public class MyClassName;
    }

    public class MyClassName;

    namespace
        MyVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongNamespace
    {
        public class MyClassName
        {
            public class MyInnerClassName;
        }
    }
}