using BenchmarkDotNet.Running;

var assembyly = typeof(Program).Assembly;

BenchmarkRunner.Run(assembyly, args: args);