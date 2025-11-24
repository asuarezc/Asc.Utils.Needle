# Asc.Utils.Needle

Asc.Utils.Needle is a small, focused library that simplifies multithreading and background work.
Using a lightweight factory (`Pincushion.Instance`) you can obtain workers that run jobs using well-known
concurrency patterns: semaphore-based workers, parallel workers and reusable job processors.

Parallel and semaphore workers implementations share the same interfaces so you can swap implementations,
mock for tests, or integrate them into DI containers.

Advantages over hand-rolling concurrency

- Unified, well-tested abstractions: no more repeating the same cancellation, synchronization anderror-aggregation code.
- Error handling: job exceptions are collected and surfaced as an `AggregateException` so you can handle failures centrally.
- Cancellation and observability: workers expose a `CancellationToken`, events and/or `INotifyPropertyChanged` (non-slim variants) to observe progress or cancellation.
- Testability: interfaces make it easy to mock behaviour in unit tests.
- Fewer subtle bugs: the library handles thread-safety, worker lifecycle and resource disposal for you.

---

## Which worker to use

Pick the worker that matches your workload and observability needs:

- Semaphore-style workers (`SemaphoreWorker`, `SemaphoreWorkerSlim`)
  - Limit the number of concurrent jobs with a semaphore. Ideal for I/O-heavy workloads (HTTP calls, database requests) where you want to bound concurrency.
  - `SemaphoreWorkerSlim` is the lightweight, high-performance variant (minimal API), suitable for backend or batch scenarios.
  - `SemaphoreWorker` (non-slim) exposes progress counters and `INotifyPropertyChanged` for UI or diagnostics.

- Parallel workers (`ParallelWorker`, `ParallelWorkerSlim`)
  - Execute jobs in parallel without an explicit concurrency limit. Good for CPU-light tasks where maximum parallelism is desired.
  - `ParallelWorkerSlim` = minimal API; `ParallelWorker` = full API with counters and events.

- Job processors (`NeedleJobProcessor`, `NeedleJobProcessorSlim`)
  - Long-lived processors with an internal thread pool that accept jobs dynamically. Use them when jobs are produced continuously and you want a reusable pool.
  - `NeedleJobProcessorSlim` = minimal, low-overhead; non-slim = additional counters and `PropertyChanged` notifications.

Slim vs non-slim

- Slim versions prioritize throughput and low allocations and offer a minimal API surface.
- Non-slim versions add observability (properties, events, `INotifyPropertyChanged`) and are useful for UIs or monitoring scenarios.

### Quick capability summary

Each worker type is listed with short capability lines to avoid wide columns and improve readability on GitHub.

- **SemaphoreWorker**
  - Bounded concurrency: Yes
  - Dynamic enqueue: No
  - `INotifyPropertyChanged`: Yes
  - Counters: Yes
  - Low-overhead: No

- **SemaphoreWorkerSlim**
  - Bounded concurrency: Yes
  - Dynamic enqueue: No
  - `INotifyPropertyChanged`: No
  - Counters: No
  - Low-overhead: Yes

- **ParallelWorker**
  - Bounded concurrency: No
  - Dynamic enqueue: No
  - `INotifyPropertyChanged`: Yes
  - Counters: Yes
  - Low-overhead: No

- **ParallelWorkerSlim**
  - Bounded concurrency: No
  - Dynamic enqueue: No
  - `INotifyPropertyChanged`: No
  - Counters: No
  - Low-overhead: Yes

- **NeedleJobProcessor**
  - Bounded concurrency: Yes
  - Dynamic enqueue: Yes
  - `INotifyPropertyChanged`: Yes
  - Counters: Yes
  - Low-overhead: No

- **NeedleJobProcessorSlim**
  - Bounded concurrency: Yes
  - Dynamic enqueue: Yes
  - `INotifyPropertyChanged`: No
  - Counters: No
  - Low-overhead: Yes

---

## Installation

Install from NuGet (dotnet CLI):

```bash
dotnet add package Asc.Utils.Needle
```

Or using the Package Manager Console:

```powershell
Install-Package Asc.Utils.Needle
```

You can also reference the package in your csproj:

```xml
<PackageReference Include="Asc.Utils.Needle" Version="2.1.0" />
```

---

## Examples

All examples use the `Pincushion.Instance` factory to obtain workers.

- Semaphore worker (slim) — bounded concurrency (backend example):

```csharp
using Asc.Utils.Needle;

using var worker = Pincushion.Instance.GetSemaphoreWorkerSlim(maxThreads: 5);

foreach (var url in urls)
    worker.AddJob(async () => await HttpGetAsync(url));

try
{
    await worker.RunAsync();
}
catch (AggregateException ae)
{
    // examine ae.InnerExceptions
}
```

- Semaphore worker (full) — with progress notifications (frontend example):

```csharp
using Asc.Utils.Needle;

using var worker = Pincushion.Instance.GetSemaphoreWorker(maxThreads: 5);

worker.PropertyChanged += (_, e) =>
{
    var prop = worker.GetType().GetProperty(e.PropertyName);
    var value = prop is not null ? prop.GetValue(worker) : null;
    Console.WriteLine($"{e.PropertyName} = {value}");
};

worker.AddJob(() => DoSyncWork());
worker.AddJob(async () => await DoAnotherWorkAsync());

await worker.RunAsync();
```

- Parallel worker (slim):

```csharp
using Asc.Utils.Needle;

using var worker = Pincushion.Instance.GetParallelWorkerSlim();

for (int i = 0; i < 100; i++)
    worker.AddJob(async () => await DoSmallAsyncWork());

await worker.RunAsync();
```

- Parallel worker (full):

```csharp
using Asc.Utils.Needle;

using var worker = Pincushion.Instance.GetParallelWorker();

worker.PropertyChanged += (_, e) =>
{
    var prop = worker.GetType().GetProperty(e.PropertyName);
    var value = prop is not null ? prop.GetValue(worker) : null;
    Console.WriteLine($"{e.PropertyName} = {value}");
};

worker.AddJob(() => DoSyncWork());

await worker.RunAsync();
```

- NeedleJobProcessorSlim — dynamic job stream with internal pool:

```csharp
using Asc.Utils.Needle;

using var processor = Pincushion.Instance.GetJobProcessorSlim(threadPoolSize: Environment.ProcessorCount);
processor.Start();

processor.ProcessJob(async () => await DoBackgroundWork());
processor.ProcessJob(SomeVoidMethodToRun);

processor.Pause();

// do some other stuff in between

processor.Resume();

// stop when done
await processor.DisposeAsync();
```

- NeedleJobProcessor (full) — same as above but with counters and property notifications:

```csharp
using Asc.Utils.Needle;

using var processor = Pincushion.Instance.GetJobProcessor(threadPoolSize: 4);

processor.PropertyChanged += (_, e) =>
{
    var prop = processor.GetType().GetProperty(e.PropertyName);
    var value = prop is not null ? prop.GetValue(processor) : null;
    Console.WriteLine($"{e.PropertyName} = {value}");
};

processor.Start();
processor.ProcessJob(() => Task.Delay(100));

await processor.DisposeAsync();
```

---

## More information

Read the public interfaces for details on behavior and guarantees:

- `INeedleWorkerSlim` / `INeedleWorker`
- `INeedleJobProcessorSlim` / `INeedleJobProcessor`

They document cancellation semantics, error aggregation and lifecycle constraints.

---

## Icon attribution

Icon from Flaticon:

<a href="https://www.flaticon.com/free-icons/" title="sew icons">See icons created by Pixel perfect - Flaticon</a>