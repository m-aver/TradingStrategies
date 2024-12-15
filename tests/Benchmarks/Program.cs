using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using TradingStrategies.Benchmarks;
using Microsoft.Diagnostics.Tracing.Parsers;
using BenchmarkDotNet.Running;

//ConcurrencyVisualizerProfiler проблемы - DirectoryNotFoundException
//переименовал директорию проекта с TradingStrategies.Benchmarks на Benchmarks, кажется помогло

internal class Program
{
    private static void RunSynchronizedBarIteratorBenchmark()
    {
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .With(Platform.AnyCpu)
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithGcForce(true)
            )
            //.AddDiagnoser(new ConcurrencyVisualizerProfiler())
            .AddDiagnoser(new EtwProfiler(new EtwProfilerConfig(kernelKeywords: KernelTraceEventParser.Keywords.All)))
            .AddDiagnoser(new MemoryDiagnoser());

        BenchmarkRunner.Run<SynchronizedBarIteratorBenchmark>(config);
    }

    private static void RunBarsDictionaryCreateBenchmark()
    {
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .With(Platform.AnyCpu)
                .WithGcForce(true)
                )
            .AddDiagnoser(new MemoryDiagnoser());

        BenchmarkRunner.Run<BarsDictionaryBenchmark_Create>(config);
    }

    private static void RunBarsDictionaryReadBenchmark()
    {
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .With(Platform.AnyCpu)
                .WithGcForce(true)
                //.WithInvocationCount()
                //.WithUnrollFactor()
                );

        BenchmarkRunner.Run<BarsDictionaryBenchmark_Read>(config);
    }

    private static void Main(string[] args)
    {
        try
        {
            //RunSynchronizedBarIteratorBenchmark();
            //RunBarsDictionaryCreateBenchmark();
            RunBarsDictionaryReadBenchmark();
        }
        finally
        {
            Console.ReadKey();
        }
    }
}
