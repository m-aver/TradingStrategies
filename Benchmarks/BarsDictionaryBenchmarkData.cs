using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingStrategies.Backtesting.Optimizers;
using TradingStrategies.Utilities;
using WealthLab;

namespace TradingStrategies.Benchmarks;

public enum BarsType
{
    WealthLab, //WealthLab.Bars
    Wrapper, //BarsWrapper - native hash (random collisions)
    WrapperNoCollision, //BarsWrapper - 1 hash - 1 bucket
    WrapperFullCollision //BarsWrapper - all hash - 1 bucket
}

public enum BarsMapType
{
    Regular, //Dictionary
    Lite, //LiteDictionary
    Frozen, //FrozenDictionary
}

internal static class BarsDictionaryBenchmarkData
{
    public static Bars[] GetBars(int count, BarsType type, BarsMapType mapType)
    {
        var bars = Enumerable
            .Range(0, count)
            .Select(x => new Bars());

        var fullCollisionMultiplier = type is BarsType.WrapperFullCollision
            ? mapType switch
            {
                BarsMapType.Regular => HashHelpers.GetPrime(count),
                BarsMapType.Lite => count,
                BarsMapType.Frozen => HashHelpers.GetPrime(count * 2),  //frozen адаптируется, не получится обеспечить коллизии
                _ => throw new NotImplementedException(),
            }
            : 0;

        bars = type switch
        {
            BarsType.WealthLab => bars,
            BarsType.Wrapper => bars.Select((b, i) => b.WithHash(b.GetHashCode())),
            BarsType.WrapperNoCollision => bars.Select((b, i) => b.WithHash(i + 1)),
            BarsType.WrapperFullCollision => bars.Select((b, i) => b.WithHash((i + 1) * fullCollisionMultiplier)),
            _ => throw new NotImplementedException(),
        };

        return bars.ToArray();
    }

    public static IReadOnlyDictionary<Bars, Node> CreateMap(Bars[] bars, Node[] nodes, BarsMapType mapType)
    {
        return mapType switch
        {
            BarsMapType.Regular => CreateRegular(bars, nodes),
            BarsMapType.Lite => CreateLite(bars, nodes),
            BarsMapType.Frozen => CreateFrozen(bars, nodes),
            _ => throw new ArgumentException(nameof(mapType)),
        };
    }

    public static Dictionary<Bars, Node> CreateRegular(Bars[] bars, Node[] nodes)
    {
        var map = new Dictionary<Bars, Node>(bars.Length);
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            var node = nodes[i];
            map[bar] = node;
        }
        return map;
    }

    public static LiteDictionary<Bars, Node> CreateLite(Bars[] bars, Node[] nodes)
    {
        var map = new LiteDictionary<Bars, Node>(bars, nodes);
        return map;
    }

    public static FrozenDictionary<Bars, Node> CreateFrozen(Bars[] bars, Node[] nodes)
    {
        var map = bars.Select((x, i) => new KeyValuePair<Bars, Node>(x, nodes[i])).ToFrozenDictionary();
        return map;
    }
}
