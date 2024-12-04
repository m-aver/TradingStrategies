using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingStrategies.Utilities;
using WealthLab;

// LiteDictionary в 4-5 раз быстрее на чтение
// на создание сопоставимо с обычным словарем как по памяти, так и по времени

namespace TradingStrategies.Benchmarks;

public class BarsDictionaryBenchmark_Read
{
    [Params(10, 100)]
    public int SeriesCount;
    [Params(1, 100)]
    public int FetchFactor;

    [ParamsAllValues]
    public BarsType BarsType;
    [ParamsAllValues]
    public BarsMapType MapType;

    private Bars[] bars = null!;
    private Node[] nodes = null!;
    private IReadOnlyDictionary<Bars, Node> map = null!;

    [GlobalSetup]
    public void PrepareBars()
    {
        bars = BarsDictionaryBenchmarkData.GetBars(SeriesCount, BarsType, MapType);
        nodes = new Node[bars.Length];
        map = BarsDictionaryBenchmarkData.CreateMap(bars, nodes, MapType);
    }

    [Benchmark]
    public void Read()
    {
        for (int i = 0; i < FetchFactor; i++)
        {
            foreach (var bar in bars)
            {
                var node = map[bar];
            }
        }
    }
}
