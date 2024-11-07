# Asc.Utils.Needle
Utility to simplify multithreading background operations

## Installation
```sh
Install-Package Asc.Utils.Needle
```
## IMasterNeedleWorker Usage
### Simply add jobs. That's all! But remember: Handling exceptions in that jobs is your responsibility
```C#
  private async Task DoTheWorkAsync(IEnumerable<string> urls)
  {
    foreach (string url in urls)
      await Pincushion.Instance.MasterNeedle.AddJobAsync(() => DoHttpRequest(url));
  }

  private void DoHttpRequest(string url)
  {
    try
    {
      DoHttpRequestInternal(url);
    }
    catch (Exception ex)
    {
      ManageException(ex);
    }
  }

  private void DoHttpRequestInternal(string url)
  {
    //... Your http request code ...
  }
```

## INeedleWorker Usage
### With using statement
```C#
  IResult someResult = null;
  using INeedleWorker needleWorker = Pincushion.Instance.GetNeedle();
  
  needleWorker.AddJob(SomeVoidMethod);
  needleWorker.AddJob(SomeAsyncTaskMethod);
  needleWorker.AddJob(async () => await DoSomethingAsync());
  
  if (someCondition)
    needleWorker.AddJob(() => { someResult = GetResult(); });
  
  await needleWorker.RunAsync();
```

### Without using statement
```C#
  IResult someResult = null;
  INeedleWorker needleWorker = Pincushion.Instance.GetNeedle(maxThreads: 2);
  
  needleWorker.AddJob(SomeVoidMethod);
  needleWorker.AddJob(SomeAsyncTaskMethod);
  needleWorker.AddJob(async () => await DoSomethingAsync());
  
  if (someCondition)
    needleWorker.AddJob(() => { someResult = GetResult(); });

  needleWorker.Completed += (object sender, EventArgs e) => needleWorker.Dispose();
  needleWorker.BeginRun();
```

### If some job fails
```C#
  using INeedleWorker needleWorker = Pincushion.Instance.GetNeedle(
    maxThreads: 2,
    cancelPendingJobsIfAnyOtherFails: false
  );

  foreach(Action job in jobs)
    needleWorker.AddJob(job);

  try
  {
    await needleWorker.RunAsync();
  }
  catch (AggregateException aggregateEx)
  {
    foreach (Exception exception in aggregateEx.InnerExceptions)
      logger.LogException(exception);
  }
  catch (Exception ex)
  {
    logger.LogException(ex, "Unexpected error while INeedleWorker.RunAsync");
  }
```

### Check progress to update UI
```C#
  string progressText;
  string statusText;
  INeedleWorker needleWorker = Pincushion.Instance.GetNeedle(1);
  
  needleWorker.AddJob(Job1);
  needleWorker.AddJob(Job2);
  needleWorker.AddJob(Job3);
  needleWorker.AddJob(Job4);
  
  needleWorker.PropertyChanged += (object sender, PropertyChangedEventArgs e) =>
  {
    if (e is null || string.IsNullOrEmpty(e.PropertyName))
      return;
  
    if (e.PropertyName == nameof(INeedleWorker.Progress))
      progressText = $"{needleWorker.Progress}%";
  
    if (e.PropertyName == nameof(INeedleWorker.CompletedJobsCount))
      statusText = $"Completed {needleWorker.CompletedJobsCount} of {needleWorker.TotalJobsCount} jobs";
  };
  
  await needleWorker.RunAsync();
```

### More info
See INeedleWorker and IMasterNeedleWorker interfaces to get more info about how to use this utility

## Icon from Flaticon:
<a href="https://www.flaticon.com/free-icons/sew" title="sew icons">Sew icons created by Pixel perfect - Flaticon</a>
