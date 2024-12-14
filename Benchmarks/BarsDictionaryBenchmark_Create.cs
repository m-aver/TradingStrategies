using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingStrategies.Utilities;
using WealthLab;

namespace TradingStrategies.Benchmarks;

public class BarsDictionaryBenchmark_Create
{
    [Params(100)]
    public int SeriesCount;

    [Params(BarsType.WealthLab, BarsType.Wrapper)]  //коллизии не сильно важны при создании
    public BarsType BarsType;
    [ParamsAllValues]
    public BarsMapType MapType;

    private Bars[] bars = null!;
    private Node[] nodes = null!;

    [GlobalSetup]
    public void PrepareBars()
    {
        bars = BarsDictionaryBenchmarkData.GetBars(SeriesCount, BarsType, MapType);
        nodes = new Node[bars.Length];
    }

    [Benchmark]
    public void Create()
    {
        var map = BarsDictionaryBenchmarkData.CreateMap(bars, nodes, MapType);
    }
}
