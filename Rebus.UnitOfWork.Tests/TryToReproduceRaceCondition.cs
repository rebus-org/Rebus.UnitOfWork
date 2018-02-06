using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Transport.InMem;
#pragma warning disable 1998

namespace Rebus.UnitOfWork.Tests
{
    [TestFixture]
    public class TryToReproduceRaceCondition : FixtureBase
    {
        readonly ConcurrentQueue<string> _eventRecorder = new ConcurrentQueue<string>();

        BuiltinHandlerActivator _activator;

        protected override void SetUp()
        {
            _eventRecorder.Clear();

            _activator = new BuiltinHandlerActivator();

            Using(_activator);

            Configure.With(_activator)
                .Logging(l => l.Console(LogLevel.Warn))
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "whatever"))
                .Options(o =>
                {
                    o.EnableUnitOfWork(
                        context => new TestUnitOfWork(_eventRecorder, context),
                        (context, uow) => uow.Commit(),
                        (context, uow) => uow.Rollback(),
                        (context, uow) => uow.Dispose()
                    );

                    o.SetNumberOfWorkers(1);
                    o.SetMaxParallelism(1);
                })
                .Start();
        }

        [TestCase(3)]
        [TestCase(30)]
        [TestCase(300)]
        [TestCase(3000)]
        [TestCase(30000)]
        public async Task EverythingHappensInSerialOrder(int numberOfMessages)
        {
            var bus = _activator.Bus;
            var counter = new SharedCounter(numberOfMessages);

            _activator.Handle<string>(async str =>
            {
                _eventRecorder.Enqueue($"Handling {str}");
                counter.Decrement();
            });

            foreach (var number in Enumerable.Range(1, numberOfMessages))
            {
                await bus.SendLocal($"{number}", new Dictionary<string, string> { { "number", number.ToString(CultureInfo.InvariantCulture) } });
            }

            counter.WaitForResetEvent(3 + numberOfMessages / 100);

            // now check that no events were interleaved at any point in time
            foreach (var batch in _eventRecorder.Batch(4))
            {
                VerifyBatch(batch);
            }
        }

        static void VerifyBatch(List<string> batch)
        {
            var events = batch
                .Select(line =>
                {
                    var parts = line.Split(' ');
                    return new
                    {
                        Event = parts[0],
                        Number = int.Parse(parts[1])
                    };
                })
                .ToArray();

            var expected = new[] { "Create", "Handling", "Commit", "Dispose" };

            Assert.That(events.Select(e => e.Event).ToArray(), Is.EqualTo(expected), $@"Event sequence within batch did not match the expected sequence:

{string.Join(Environment.NewLine, batch)}
");
            
            var expectedNumber = events.First().Number;
            
            Assert.That(events.All(e => e.Number == expectedNumber), $@"Not all numbers in this batch were equal:
{string.Join(Environment.NewLine, batch)}
");

        }

        class TestUnitOfWork
        {
            readonly ConcurrentQueue<string> _eventRecorder;
            readonly int _messageNumber;

            public TestUnitOfWork(ConcurrentQueue<string> eventRecorder, IMessageContext context)
            {
                _messageNumber = int.Parse(context.TransportMessage.Headers.GetValue("number"));
                _eventRecorder = eventRecorder;
                _eventRecorder.Enqueue($"Create {_messageNumber}");
            }

            public void Commit() => _eventRecorder.Enqueue($"Commit {_messageNumber}");

            public void Rollback() => _eventRecorder.Enqueue($"Rollback {_messageNumber}");

            public void Dispose() => _eventRecorder.Enqueue($"Dispose {_messageNumber}");
        }
    }
}