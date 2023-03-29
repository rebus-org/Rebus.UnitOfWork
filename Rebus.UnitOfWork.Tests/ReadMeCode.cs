using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Pipeline;
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
            using var activator = new BuiltinHandlerActivator();
            
            Configure.With(activator)
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "whatever"))
                .Options(o => o.EnableAsyncUnitOfWork(
                    create: async context => new MyDbContext(),
                    commit: async (context, uow) => await uow.SaveChangesAsync(),
                    dispose: async (context, uow) => uow.Dispose()
                ))
                .Start();
        }

        [Test]
        public void Example2()
        {
            using var activator = new BuiltinHandlerActivator();
            
            Configure.With(activator)
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "whatever"))
                .Options(o => o.EnableAsyncUnitOfWork(
                    create: async context =>
                    {
                        var uow = new MyDbContext();
                        context.TransactionContext.Items["current-uow"] = uow;
                        return uow;
                    },
                    commit: async (context, uow) => await uow.SaveChangesAsync(),
                    dispose: async (context, uow) => uow.Dispose()
                ))
                .Start();
        }

        [Test]
        public void ContainerCode()
        {
            var services = new ServiceCollection();

            services.AddScoped(p =>
            {
                var context = p.GetService<IMessageContext>() 
                              ?? throw new InvalidOperationException("Cannot resolve db context outside of Rebus handler, sorry");

                return context.TransactionContext.Items.TryGetValue("current-uow", out var result)
                    ? (MyDbContext)result
                    : throw new ArgumentException("Didn't find db context under 'current-uow' key in current context");

            });
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