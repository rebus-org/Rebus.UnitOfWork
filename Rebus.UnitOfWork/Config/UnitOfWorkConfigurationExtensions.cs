using System;
using System.Threading.Tasks;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.UnitOfWork;

// ReSharper disable ArgumentsStyleAnonymousFunction
#pragma warning disable 1998

namespace Rebus.Config
{
    /// <summary>
    /// Configuration extensions for the unit of work API
    /// </summary>
    public static class UnitOfWorkConfigurationExtensions
    {
        /// <summary>
        /// Wraps the invocation of the incoming pipeline in a step that creates a unit of work, committing/rolling back depending on how the invocation of the pipeline went. The cleanup action is always called.
        /// </summary>
        public static void EnableUnitOfWork<TUnitOfWork>(this OptionsConfigurer configurer,
            Func<IMessageContext, TUnitOfWork> create,
            Action<IMessageContext, TUnitOfWork> commit,
            Action<IMessageContext, TUnitOfWork> rollback = null,
            Action<IMessageContext, TUnitOfWork> dispose = null)
        {
            if (create == null) throw new ArgumentNullException(nameof(create), "You need to provide a factory method that is capable of creating new units of work");
            if (commit == null) throw new ArgumentNullException(nameof(commit), "You need to provide a commit action that commits the current unit of work");

            configurer.EnableAsyncUnitOfWork(
                create: async context => create(context),
                commit: async (context, unitOfWork) => commit(context, unitOfWork),
                rollback: async (context, unitOfWork) => rollback?.Invoke(context, unitOfWork),
                dispose: async (context, unitOfWork) => dispose?.Invoke(context, unitOfWork)
            );
        }

        /// <summary>
        /// Wraps the invocation of the incoming pipeline in a step that creates a unit of work, committing/rolling back depending on how the invocation of the pipeline went. The cleanup action is always called.
        /// </summary>
        public static void EnableAsyncUnitOfWork<TUnitOfWork>(this OptionsConfigurer configurer,
            Func<IMessageContext, Task<TUnitOfWork>> create,
            Func<IMessageContext, TUnitOfWork, Task> commit,
            Func<IMessageContext, TUnitOfWork, Task> rollback = null,
            Func<IMessageContext, TUnitOfWork, Task> dispose = null)
        {
            if (create == null) throw new ArgumentNullException(nameof(create), "You need to provide a factory method that is capable of creating new units of work");
            if (commit == null) throw new ArgumentNullException(nameof(commit), "You need to provide a commit action that commits the current unit of work");

            configurer.Decorate<IPipeline>(context =>
            {
                var pipeline = context.Get<IPipeline>();
                var unitOfWorkStep = new UnitOfWorkStep<TUnitOfWork>(create, commit, rollback, dispose);

                return new PipelineStepInjector(pipeline)
                    .OnReceive(unitOfWorkStep, PipelineRelativePosition.Before, typeof(ActivateHandlersStep));
            });
        }
    }
}
