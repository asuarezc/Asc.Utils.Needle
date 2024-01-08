# Asc.Utils.Needle
Utility to simplify multithreading background operations

## Installation
```sh
Install-Package Asc.Utils.Needle
```

## Usage
### With using statement
```C#
  IResult someResult = null;
  using INeddleWorker neddleWorker = Pincushion.Instance.GetNeedle();
  
  neddleWorker.AddJob(SomeVoidMethod);
  neddleWorker.AddJob(SomeAsyncTaskMethod);
  neddleWorker.AddJob(async () => await DoSomethingAsync());
  
  if (someCondition)
    neddleWorker.AddJob(() => { someResult = GetResult(); });
  
  await neddleWorker.RunAsync();
```

### Without using statement
```C#
  IResult someResult = null;
  INeddleWorker neddleWorker = Pincushion.Instance.GetNeedle(maxThreads: 2);
  
  neddleWorker.AddJob(SomeVoidMethod);
  neddleWorker.AddJob(SomeAsyncTaskMethod);
  neddleWorker.AddJob(async () => await DoSomethingAsync());
  
  if (someCondition)
    neddleWorker.AddJob(() => { someResult = GetResult(); });

  needleWorker.Completed += (object sender, EventArgs e) => needleWorker.Dispose();
  neddleWorker.BeginRun();
```

### If some job fails
```C#
  using INeddleWorker neddleWorker = Pincushion.Instance.GetNeedle(
    maxThreads: 2,
    cancelPendingJobsIfAnyOtherFails: false
  );

  foreach(Action job in jobs)
    neddleWorker.AddJob(job);

  try
  {
    await neddleWorker.RunAsync();
  }
  catch (AggregateException aggregateEx)
  {
    foreach (Exception exception in aggregateEx.InnerExceptions)
      logger.LogException(exception);
  }
  catch (Exception ex)
  {
    logger.LogException(ex, "Unexpected error while INeddleWorker.RunAsync");
  }
```

### Check progress to update UI
```C#
  string progressText;
  string statusText;
  INeddleWorker neddleWorker = Pincushion.Instance.GetNeedle(1);
  
  neddleWorker.AddJob(Job1);
  neddleWorker.AddJob(Job2);
  neddleWorker.AddJob(Job3);
  neddleWorker.AddJob(Job4);
  
  neddleWorker.PropertyChanged += (object sender, PropertyChangedEventArgs e) =>
  {
    if (e is null || string.IsNullOrEmpty(e.PropertyName))
      return;
  
    if (e.PropertyName == nameof(INeddleWorker.Progress))
      progressText = $"{neddleWorker.Progress}%";
  
    if (e.PropertyName == nameof(INeddleWorker.CompletedJobsCount))
      statusText = $"Completed {neddleWorker.CompletedJobsCount} of {neddleWorker.TotalJobsCount} jobs";
  };
  
  await neddleWorker.RunAsync();
```

### More info
See INeddleWorker interface to get more info about how to use this utility

## Icon from Flaticon:
<a href="https://www.flaticon.com/free-icons/sew" title="sew icons">Sew icons created by Pixel perfect - Flaticon</a>
