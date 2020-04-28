using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Transport.InMem;

namespace Rebus.UnitOfWork.Tests
{
    [TestFixture]
    public class ReadMeCode : FixtureBase
    {
        [Test]
        public void Example1()
        {
            using (var activator = new BuiltinHandlerActivator())
            {

                Configure.With(activator)
                    .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "whatever"))
                    //.Options(o => o.EnableUnitOfWork())
                    .Start();
            }
        }
    }
}