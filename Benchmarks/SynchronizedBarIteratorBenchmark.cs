using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using System.Runtime.CompilerServices;
using WealthLab;

namespace TradingStrategies.Benchmarks;

public class SynchronizedBarIteratorBenchmark
{
    [Params(10)]
    public int SeriesCount;
    [Params(1000)]
    public int BarsCount;

    [Benchmark]
    public void RunOld()
    {
        IList<Bars> barsCollection = SynchronizedBarIteratorBenchmarkData.GetLinearBars(SeriesCount, BarsCount);

        var iterator = new WealthLab.SynchronizedBarIterator(barsCollection);
        do
        {
            var date = iterator.Date;

            foreach (var bars in barsCollection)
            {
                var bar = iterator.Bar(bars);
            }
        }
        while (iterator.Next());
    }

    [Benchmark]
    public void RunOwn()
    {
        IList<Bars> barsCollection = SynchronizedBarIteratorBenchmarkData.GetLinearBars(SeriesCount, BarsCount);

        var iterator = new TradingStrategies.Utilities.SynchronizedBarIterator(barsCollection);

        do
        {
            var date = iterator.Date;

            foreach (var bars in barsCollection)
            {
                var bar = iterator.Bar(bars);
            }
        }
        while (iterator.Next());
    }

    //TODO: разное количество тредов
    [Benchmark]
    public void RunOwnConcurrently()
    {
        IList<Bars> barsCollection = SynchronizedBarIteratorBenchmarkData.GetLinearBars(SeriesCount, BarsCount);

        int treads = Environment.ProcessorCount;

        //с Parallel.ForEach не особо показательно для ConcurrencyVisualizerProfiler
        Parallel.ForEach(
            source: Enumerable.Range(0, treads),
            body: (_) => Run(barsCollection));

        static void Run(ICollection<Bars> barsCollection)
        {
            var iterator = new TradingStrategies.Utilities.SynchronizedBarIterator(barsCollection);
            do
            {
                var date = iterator.Date;

                foreach (var bars in barsCollection)
                {
                    var bar = iterator.Bar(bars);
                }
            }
            while (iterator.Next());
        }
    }
}
