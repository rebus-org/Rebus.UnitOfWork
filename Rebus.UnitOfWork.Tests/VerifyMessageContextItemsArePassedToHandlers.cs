using System;
using System.Threading;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Pipeline;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Transport.InMem;
#pragma warning disable 1998

namespace Rebus.UnitOfWork.Tests
{
    [TestFixture]
    public class VerifyMessageContextItemsArePassedToHandlers : FixtureBase
    {
        [Test]
        public void Yes()
        {
            using (var activator = new BuiltinHandlerActivator())
            {
                var gotMessage = new ManualResetEvent(false);

                activator.Handle<string>(async (bus, context, message) =>
                {
                    var uow = (Uow)context.TransactionContext.Items["uow"];

                    if (uow == null)
                    {
                        throw new RebusApplicationException("uow was null");
                    }

                    gotMessage.Set();
                });

                Configure.With(activator)
                    .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "uow-test"))
                    .Options(o => o.EnableUnitOfWork(Create, (context, uow) => uow.Commit()))
                    .Start();

                activator.Bus.Advanced.SyncBus.SendLocal("hej med dig min ven!");

                gotMessage.WaitOrDie(TimeSpan.FromSeconds(3));
            }
        }

        static Uow Create(IMessageContext context)
        {
            var uow = new Uow();

            context.TransactionContext.Items["uow"] = uow;

            return uow;
        }

        class Uow
        {
            public void Commit()
            {
            }
        }
    }
}