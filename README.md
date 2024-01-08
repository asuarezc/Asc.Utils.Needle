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
