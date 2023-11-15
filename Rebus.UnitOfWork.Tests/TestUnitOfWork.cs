using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Transport.InMem;
// ReSharper disable ArgumentsStyleAnonymousFunction
#pragma warning disable 1998

namespace Rebus.UnitOfWork.Tests
{
    [TestFixture]
    public class TestUnitOfWork : FixtureBase
    {
        const string UowQueueName = "uow-test";
        const string OtherQueueName = "uow-test-recipient";

        ConcurrentQueue<string> _events;
        BuiltinHandlerActivator _uowActivator;
        BuiltinHandlerActivator _otherActivator;
        IBusStarter _otherStarter;
        IBusStarter _uowStarter;

        protected override void SetUp()
        {
            var network = new InMemNetwork();

            _events = new ConcurrentQueue<string>();
            _uowActivator = new BuiltinHandlerActivator();
            _otherActivator = new BuiltinHandlerActivator();

            Using(_uowActivator);
            Using(_otherActivator);

            _uowStarter = Configure.With(_uowActivator)
                .Logging(l => l.Console(LogLevel.Warn))
                .Transport(t => t.UseInMemoryTransport(network, UowQueueName))
                .Options(o =>
                {
                    o.EnableUnitOfWork(c => _events,
                        commit: (c, e) => RegisterEvent("uow committed"),
                        rollback: (c, e) => RegisterEvent("uow rolled back"),
                        dispose: (c, e) => RegisterEvent("uow cleaned up")
                    );

                    o.RetryStrategy(maxDeliveryAttempts: 1);

                    //o.LogPipeline(true);
                })
                .Create();

            _otherStarter = Configure.With(_otherActivator)
                .Logging(l => l.Console(LogLevel.Warn))
                .Transport(t => t.UseInMemoryTransport(network, OtherQueueName))
                .Create();
        }

        [Test]
        public async Task CommitsBeforeSendingMessages()
        {
            var counter = new SharedCounter(1);

            _otherActivator.Handle<string>(async str =>
            {
                RegisterEvent("message sent from uow-enabled endpoint was handled");
            });

            _uowActivator.Handle<string>(async (bus, str) =>
            {
                RegisterEvent("uow-message handled");

                await bus.Advanced.Routing.Send(OtherQueueName, "woohooo!!!");

                counter.Decrement();
            });

            var uowBus = _uowStarter.Start();
            _otherStarter.Start();

            RegisterEvent("message sent");

            await uowBus.SendLocal("hej med dig min veeeeeen!");

            counter.WaitForResetEvent();

            await Task.Delay(1000);

            var events = _events.ToArray();

            var expectedEvents = new[]
            {
                "message sent",
                "uow-message handled",
                "uow committed",
                "uow cleaned up",
                "message sent from uow-enabled endpoint was handled"
            };

            Assert.That(events, Is.EqualTo(expectedEvents));
        }

        [Test]
        public async Task OutgoingMessagesAreNotSentWhenRollingBack()
        {
            _otherActivator.Handle<string>(async str =>
            {
                RegisterEvent("message sent from uow-enabled endpoint was handled");
            });

            _uowActivator.Handle<string>(async (bus, str) =>
            {
                RegisterEvent("uow-message handled");

                await bus.Advanced.Routing.Send(OtherQueueName, "woohooo!!!");

                throw new InvalidOperationException("bummer, dude!");
            });

            var uowBus = _uowStarter.Start();
            _otherStarter.Start();

            RegisterEvent("message sent");

            await uowBus.SendLocal("hej med dig min veeeeeen!");

            await Task.Delay(2000);

            var events = _events.ToArray();

            var expectedEvents = new[]
            {
                "message sent",
                "uow-message handled",
                "uow rolled back",
                "uow cleaned up",
            };

            Assert.That(events, Is.EqualTo(expectedEvents));
        }

        void RegisterEvent(string description)
        {
            _events.Enqueue(description);
        }
    }
}
