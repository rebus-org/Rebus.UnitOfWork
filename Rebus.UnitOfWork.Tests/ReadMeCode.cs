using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Transport.InMem;
// ReSharper disable ArgumentsStyleAnonymousFunction
#pragma warning disable 1998

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
                    .Options(o => o.EnableAsyncUnitOfWork(
                        create: async context => new MyDbContext(),
                        commit: async (context, uow) => await uow.SaveChangesAsync(),
                        dispose: async (context, uow) => uow.Dispose()
                    ))
                    .Start();
            }
        }

        class MyDbContext : IDisposable
        {
            public void Dispose()
            {
            }

            public async Task SaveChangesAsync()
            {
            }
        }
    }
}