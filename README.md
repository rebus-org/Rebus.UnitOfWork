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

if you want a unit of work that supports asynchronous