# Asc.Utils.Needle
Utility to simplify multithreading or background operations. Through a simple factory pattern,
you can obtain a helper that works as a semaphore or simply performs parallel execution.

All provided utilities implement the same interfaces, allowing you to change the implementation
at runtime, use dependency injection, register the implementations as services in an IoC manager
or perform mocks for unit testing.

For backend applications or applications that simply do not require user feedbackg,
there are slim versions of both utilities. There slim versions are performance oriented.

For frontend applications, you have properties that report the execution status, the total
number of tasks to be executed, the total number of executed tasks (failed or not), and other
information to give feedback to the user in the form of a progress bar or the desired layout.
You do not need to actively check for property values since you can subscribe PropertyChanged
event.

All available utilities support runtime cancellation, provide a cancellation token, and support
both synchronous and asynchronous tasks. You can decide whether pending tasks should be
cancelled once a previous task has failed or whether the error should be ignored and the
remaining tasks completed. If one or more tasks in the batch fail, you can catch an
AggregateException with all exceptions thrown during a multithreaded execution.

## When to use a sempahore or parallel execution?
The answer is simple: run performance tests. It's the only way to know for sure.
However, as a general rule, parallel execution is better when the number of tasks does not
exceed the number of processors available on the device you are running this utility on.
For all other cases, a semaphore is better. You should also consider whether your app will be
the only thing running on a given device or whether it shares resources with other apps.

## Installation
```sh
Install-Package Asc.Utils.Needle
```
## Usage
### Simply add jobs. That's all!
```C#
  private async Task DoTheWorkAsync(IEnumerable<string> urls)
  {
    using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim(maxThreads: 5);

    foreach (string url in urls)
      worker.AddJob(async () => await DoHttpRequestAsync(url));

    try
    {
      await worker.RunAsync();
    }
    catch (AggregateException aggregateException) //When one or more of your http request fails
    {
        //Your code to manage aggregateException. Check for InnerExceptions property.
    }
    catch (Exception ex) //When an unhandled exception has been thrown
    {
        //Your code to manage ex
    }
  }

  private async Task DoHttpRequestAsync(string url)
  {
    //Your http request code
  }
```

### Cancel and CancellationToken example
```C#
  private void BeginDoTheWork(IEnumerable<string> urls, TimeSpan timeout)
  {
    bool wasCanceled = false;
    INeedleWorker worker = Pincushion.Instance.GetParallelWorker();

    void OnCanceled(object? sender, EventArgs e)
    {
      wasCanceled = true;
      Console.WriteLine("Timeout!");
    }

    worker.Canceled += OnCanceled;

    foreach (string url in urls)
      worker.AddJob(async () => await DoHttpRequestAsync(url, worker.CancellationToken));

    Task.Run(async () => {
      await worker.RunAsync();

      if (!wasCanceled)
      {
        Console.WriteLine("Completed!");
      }

      worker.Dispose();
    });

    Task.Run(async () => {
      await Task.Delay(timeout);

      if (worker != null && worker.IsRunning)
        worker.Cancel();
    });
  }

  private async Task DoHttpRequestAsync(string url, CancellationToken token)
  {
    //some code
    
    if (token.IsCancellationRequested)
      return;

    //some code
  }
```

### Check progress to update UI
```C#
  string statusText;
  INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker(maxThreads: 2);
  
  worker.AddJob(Job1);
  worker.AddJob(Job2);
  worker.AddJob(Job3);
  worker.AddJob(Job4);
  
  worker.PropertyChanged += (object sender, PropertyChangedEventArgs e) =>
  {
    if (e is null || string.IsNullOrEmpty(e.PropertyName))
      return;
  
    if (e.PropertyName == nameof(INeedleWorker.CompletedJobsCount))
      statusText = $"Completed {worker.CompletedJobsCount} of {worker.TotalJobsCount} jobs";
  };
  
  await worker.RunAsync();
```

## More info
See INeedleWorker and INeedleWorkerSlim interfaces to get more info about how to use this utility

## Icon from Flaticon:
<a href="https://www.flaticon.com/free-icons/sew" title="sew icons">Sew icons created by Pixel perfect - Flaticon</a>