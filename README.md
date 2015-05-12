# Akka.Contrib.AsyncActors

This project provides 3 actors with better support for async/await for Akka.NET:

* AsyncUntypedActor
* AsyncReceiveActor
* AsyncFSM

> Note: This new version doesn't require stashing anymore.

For information on why this package is required or why it should be preferred over the official akka support for async/await, [see my reasons below](#reasons).

## Installation

Install from [Nuget](https://www.nuget.org/packages/Akka.Contrib.AsyncActors/):

```
Install-Package Akka.Contrib.AsyncActors
```

## Usage

In order to use these actors, just inherit from them as you would from the original actors, but use the async methods instead. Execution will pause/resume only if you execute async code inside the method, and will behave exactly like a synchronous method if you don't await.

```cs
class A : AsyncUntypedActor
{
    protected override async Task OnReceiveAsync(object message)
    { ... }
}

class B : AsyncReceiveActor
{
    public B()
    {
        ReceiveAsync<string>(async s => ... );
    }
}

class C : AsyncFSM<int, object>
{
    public C()
    {
        WhenAsync(0, async e => { ... });
    }
}
```

**Note:** When using AsyncReceiveActor and AsyncFSM, you can mix sync and async methods, there are no differences in performance or behavior when using sync methods.

### Context Flow

If you need any context information after `await`, you need to keep it yourself by storing it in a local variable. I believe this is a small price to pay to have correct async/await support. [See my full explanation on why below](#reasons)

```cs
ReceiveAsync<string>(async s => {

    // store the context so you can use after await
    var self = Self;
    var sender = Sender;

    var result = await DoSomethingAsync(s);

    // use the stored context instead of Self/Sender
    sender.Tell(result, self);
});

```

Checkout the [Sample Project](https://github.com/nvivo/akka-async-actors/tree/master/Src/Akka.Contrib.AsyncActors.Samples) folder for more detailed examples.

<a name="reasons"></a>
## Why having this library if Akka 1.0 supports async/await?

**TL;DR**

Akka.NET supports only ReceiveActor, and behaves differently from what you'd expect. This library adds support to the 3 major actors (UntypedActor, ReceiveActor and FSM) and provides the expected behavior for async/await.

Now, the full explanation:

---

Akka.NET 1.0 added support for async/await in ReceiveActor, by using a task-returning overload:

```cs
class Actor : ReceiveActor {
    public Actor() {
        Receive<string>(async s => {
          Sender.Tell(await DoSomethingAsync(s))
        });
    }
}
```

The issue with this method is that it behaves very differently from what you'd expect.

The way Akka.NET currently handles an async receive is:

1. Encapsulate your method in a task
2. Pause the mailbox, so no messages are processed during method execution
3. Dispatch the method to execute in the actor context and await for it to complete (using the expected flow below)
4. On complete, send a message to the actor to finish execution
5. Resume the mailbox

There is a lot of overhead there caused by [`RunTask`](https://github.com/akkadotnet/akka.net/blob/5f2dd97064c73185de6e5ed3a96f0a4b5aebc7fb/src/core/Akka/Dispatch/ActorTaskScheduler.cs#L102), but an even bigger issue is that the implementation assumes that async methods should *run as tasks* instead of *awaited*. This is a major drawback in my opinion.

All we really needed was part of item 3, the "await for it to complete" part.

This is what happens when .NET runs an `async` method.

1. Start executing your method
2. If there is an `await task`, check if `task` is completed
3. If the task is completed, continue as if nothing happened
4. If the task is not completed (it is already scheduled or running somewhere), schedule the rest of the method to execute when that task is completed

The most important thing here is that **the behavior of an async method with a completed or non-existent task is exactly the same as of a non-async method**. That is, these 2 methods are identical in behavior:

```cs
void Foo() {
}

async Task Foo() {
}
```

When executed, both methods run from beggining to end in the same thread with the same performance, no continuations or context flows happen. Their performance should be identical (except of course for some minor differences to the async state machine that can be detected in micro-benchmarks only, those are mostly ifs and switch statements). And this doesn't depend on `await` being used in the code, it depends only on a task it awaits not being completed.

That means, this code is synchronous:

```cs
async Task Foo() {
  await Task.Delay(0);
}
```

but this one is not:

```cs
async Task Foo() {
  await Task.Delay(1);
}
```

This is important because the purpose of async/await is to allow code to be written as if it was synchronous. You may not know if you need to await until runtime. For example:

```cs
Receive<Request>(async request => {

  var response = GetCachedResponse(request);

  if (response == null)
      response = await GetResponseAndAddToCache(request);

  Sender.Tell(response);
});
```

This code behaves exactly like a regular method if the response is cached, and awaits asynchronously only if the value is not cached. Imagine now your cache expires every 10 seconds, and you can handle 1 million requests/second.

The current Akka.NET code always creates and schedules a task, and adds the overhead of an extra message for every message you process using an async handler. That is, the throughput with async/await code will always decrease by 50% at least due to an extra message for every message it handles, and a little bit more due to unnecessary scheduling.

In the example above, you'd be scheduling tasks and sending extra messages for every message even if your actual need to do await happens only once every 10.000.000 messages.

This library will behave as expected, that is:

1. Execute your handler and check the task you returned
2. If the task is completed, return immediately and cause no extra overhead
3. If the task is not completed, pause the mailbox, wait for it to finish and resume

No extra messages are sent during await.

The caveat is that due to how the Akka.NET TaskScheduler works currently, it's not possible to keep the context flowing through awaits without changes to Akka.NET itself, and you have to do it yourself as explained above. I do believe however that this is a very small price to pay to have the correct behavior and performance of async/await.

## Finally

I have been trying to push this to Akka.NET with no success so far, so I hope more people notice the issue with the current behavior and push for this change and better support for async/await in Akka.NET.

Akka.Typed (a new module coming) should remove the need for implicit context, but doesn't solve any of the async/await problems unless we have mailboxes that support delivering to task-returning actors. That is, this should be fixed by changing the API of ActorBase to always return a task, and at that point any actor should work with async/await with no performance loss and no need to pause and resume mailboxes.

Maybe this can be achieved in Akka.NET 2.0, but only if more people notice the issue.
