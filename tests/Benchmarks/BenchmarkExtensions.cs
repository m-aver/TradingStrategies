using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

internal static class BenchmarkExtensions
{
    public static ManualConfig AddJob(this ManualConfig config, Job job)
    {
        config.Add(job);
        return config;
    }

    public static IConfig AddDiagnoser(this IConfig config, IDiagnoser diagnoser)
    {
        return config.With(diagnoser);
    }
}