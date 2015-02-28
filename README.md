# Akka.Contrib.AsyncActors

This package provides some implementations of Akka.NET actors that support async/await calls to process messages.

> The code is provided as is, more as a proof of concept than production material. It is
simple enough that you can check what it's doing and decide for yourself though.

### Installation

Install from [Nuget](https://www.nuget.org/packages/Akka.Contrib.AsyncActors/):

```
Install-Package Akka.Contrib.AsyncActors
```

### What is provided

There are 2 async actors that work by stashing messages until the processing is finished.
This allows you to use the async/await pattern while still being non-blocking and keep the
behavior of one message at a time.

The actors are:

* **AsyncUntypedActor**

Replaces the `OnReceive` for an `OnReceiveAsync` method:

```csharp
public class MyActor : AsyncUntypedActor
{
  protected override Task OnReceiveAsync(object request)
  {
    ...
  }
}
```

* **AsyncFSM**

Provides a new `WhenAsync` method:

```csharp
public class MyFSM : FSM<int, object> {
    public MyFSM() {
      StartWith(0, null);
      WhenAsync(0, async e => {
         ...
         return GoTo(1);
      });
    }
}
```

Checkout the [Sample Project](#) folder for more detailed examples.

### Why?

Akka.NET is a great framework that makes it possible to create fast message processing handlers.
If you create your actors following certain patterns you can achieve very high throughput.
This is usually doing any processing in the background and "piping" messages to yourself.

```csharp
void Receive(object message) {
  // start some task in the background
  var task = StartProcessing(message);

  // send a message to yourself when its done
  task.PipeTo(Self);
}
```

This pattern guarantees the actor is almost always free to process new messages as fast as it can,
delegating all the work to background tasks and never blocking. But it also creates a problem
for cases where throughput is not an issue, but state is. This pattern achieves high throughput by
forcing the actor to process new messages even for cases when it can't or shouldn't.

Async/await is a language feature that solves exactly this problem, and it should be used when
you can't do anything until some task is finished, but you don't want to block the thread either.


```csharp
async Task Receive(object message) {
  await Process(message);
  // do something else
}
```

### So, this means "async everywhere"?

Definitely not! This means you can use this solution if you need to process a message
asynchronously and don't want to process anything until that is finished. That's all.

In fact, you can mix async/await and PipeTo and it makes perfect sense to do so.
If this doesn't makes sense for your actors, don't use it.

### But Akka is already asynchronous! Isn't this solved already?

In most cases, the reentrant behavior is desired, but there are many cases where it isn't.
And when it isn't, you need to write a lot of boilerplate code to make it work the way you want.
This is exactly what this library is doing, putting all the boilerplace code in base classes so
you don't have to mix it with your logic.

### Ok, but why exactly should I use this?

Nowadays, most of the .NET framework support Tasks and C# has a language feature to make it easy
to reason about them, async/await. This in turn makes people create APIs that rely heavily on tasks.

If you are creating something new in .NET, there is a good change you already have something like:

```csharp
interface IProductStore {
  Task<Product> GetProduct(int productId);
  Task UpdateStock(int productId, int quantity);
}
```

This pattern is being used by most new .NET APIs. Everything is a task, and that makes sense
because tasks don't need to be asynchronous. You can have an implementation as:

```csharp
class MemoryProductStore: IProductStore {

  List<Product> _products = new List<Product>();

  public Task<Product> GetProduct(int productId)
  {
    var p = _products.SingleOrDefault(p => p.ID == productId);
    return Task.FromResult(p);
  }

  Task UpdateStock(int productId, int amount)
  {
    var p = GetProduct(productId);

    if (p != null)
      p.Stock += amount;

    return Task.CompletedTask;
  }
}
```

But you could also implement an ExternalProductStore that stores data somewhere on the internet
and keep your API the same

Now, imagine an actor that has this logic:

```csharp
async Task Receive(PurchaseRequest request) {

    var clientTask = clientStore.GetClient(request.ClientId);
    var productTask = productStore.GetProduct(request.ProductId);

    await Task.WhenAll(clientTask, productTask);

    var client = await clientTask;
    var product = await productTask;

    if (client.CanPurchase && product.Stock > 0) {
        await productStore.UpdateStock(product.ID, -1);
    } else {
        ...
    }
}
```

Using the PipeTo method, you should break this processing into at least 3 messages and handle
all the stashing and type checking yourself, even though you obviously cannot process another
request until this one is finished. This would increase a lot the code, and using a
MemoryProductStore this doesn't require any asynchronous calls, everything runs just like
regular synchronous code.

In my opinion, adding message passing to this logic is a waste of time. No matter how fast
Akka can pass messages around, sending a message you don't need to is not helping anything,
and can be better solved by using a tool that already exists and handles this better with
no need to invent imaginary messages to make it work, that is async/await.

### But you can encapsulate it in method/child actor/whatever!

Moving the problem to another place doesn't make it disappear.
Also, it's just an example. This could be the child actor code already.

### But... but... but Akka... this is wrong! That's not the Akka way! Every other way to code in the universe is completely outdated since Akka came into existence!

Things can be done in many different ways. Akka promotes non-blocking, but non-blocking means
no thread should be blocked to wait something, not that all actors needs to keep processing stuff
non-stop.

I believe there is nothing wrong with the concept. But I'd love to be proved wrong though.

---

Feel free to open an issue or contact me.