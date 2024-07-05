# Rebus.UnitOfWork

[![install from nuget](https://img.shields.io/nuget/v/Rebus.UnitOfWork.svg?style=flat-square)](https://www.nuget.org/packages/Rebus.UnitOfWork)

Provides a unit of work helper for [Rebus](https://github.com/rebus-org/Rebus).

![](https://raw.githubusercontent.com/rebus-org/Rebus/master/artwork/little_rebusbus2_copy-200x200.png)

---

The unit of work helper works with C# generics and lets you represent your unit of work as anything that makes sense to you.

You configure it like this:

```csharp
Configure.With(activator)
    .Transport(t => t.Use(...))
    .Options(o => o.EnableUnitOfWork(...))
    .Start();
```

for the synchronous version, or

```csharp
Configure.With(activator)
    .Transport(t => t.Use(...))
    .Options(o => o.EnableAsyncUnitOfWork(...))
    .Start();
```

if you want a unit of work that supports asynchronous creation, completion, etc.

An example could be an Entity Framework database context, `MyDbContext`, which you then manage like this:

```csharp
Configure.With(activator)
    .Transport(t => t.Use(...))
    .Options(o => o.EnableAsyncUnitOfWork(
        create: async context => new MyDbContext(),
        commit: async (context, uow) => await uow.SaveChangesAsync(),
        dispose: async (context, uow) => uow.Dispose()
    ))
    .Start();
```

By the power of C# generics, `uow` passed to the `commit` and `dispose` functions above will have the same type as
the one returned from the `create` method.

`context` will be the current `IMessageContext`, which is also statically accessible via `MessageContext.Current`,
this way enabling injection of your unit of work by using the message context to share it:
```csharp
Configure.With(activator)
    .Transport(t => t.Use(...))
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
```
and then you can configure your IoC container to be able to inject `MyDbContext` - e.g. with Microsoft Extensions Dependency Injection like this:

```csharp
services.AddScoped(p =>
{
    var context = p.GetService<IMessageContext>() 
                    ?? throw new InvalidOperationException("Cannot resolve db context outside of Rebus handler, sorry");

    return context.TransactionContext.Items.TryGetValue("current-uow", out var result)
        ? (MyDbContext)result
        : throw new ArgumentException("Didn't find db context under 'current-uow' key in current context");

});

```
