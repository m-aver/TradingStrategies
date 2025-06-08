using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using WealthLab;

//синхронизирует итерацию нескольких серий баров
//таким образом что пересекающиеся по времени серии итерируются параллельно друг другу

namespace TradingStrategies.Utilities
{
    internal class Node
    {
        public int iteration;
        public long[] ticks;
        public int count;

        public Node next;
        public Node prev;
    }

    /// <summary>
    /// Optimized version of WealthLab.SynchronizedBarIterator.
    /// Replace it in WealthLab.dll source code using ildasm.exe/ilasm.exe
    /// </summary>
    public class SynchronizedBarIterator
    {
        private long iterationTicks;
        private readonly DateTimeKind iterationTicksKind;
        private static readonly long maxTicks = DateTime.MaxValue.Ticks;

        private readonly IReadOnlyDictionary<Bars, Node> barsMap;
        private readonly int barsCount;

        private Node seek;
        private readonly Node[] nodes;
        private readonly ArrayPool<Node> nodePool;
        private readonly ArrayPool<long> longPool;

        //дата текущей итерации
        //соответствует дате текущего бара одной (или нескольких) серии,
        //либо лежит между текущей и следующей итерацией остальных серий
        //если они уже начали и еще не закончили итерирование
        public DateTime Date => new DateTime(iterationTicks, iterationTicksKind);

        //номер бара на текущей итерации данной серии
        //-1 если серия еще не начала итерироваться (лежит в будущем)
        //или номер последнего бара если уже закончила итерирование (осталась в прошлом)
        //(внутренние итерации смещены на +1 для оптимизации)
        public int Bar(Bars bars) => barsMap[bars].iteration - 1;

        public SynchronizedBarIterator(ICollection<Bars> barCollection)
        {
            barsCount = barCollection.Count;

            if (barsCount == 0)
            {
                var msg = $"this implementation of {nameof(SynchronizedBarIterator)} does not support full empty dataset";
                throw new ArgumentException(msg, nameof(barCollection));
            }

            longPool = ArrayPoolHelper<long>.PreconfiguredShared;
            nodePool = ArrayPoolHelper<Node>.PreconfiguredShared;

#if DEBUG   //сильно тупит в дебаге из студии, видимо влияет SpinLock(Debugger.IsAttached) в бакете пула
            nodePool = NodePool.CreateOrGet(barsCount);
#endif
            nodes = nodePool.Rent(barsCount);

            iterationTicks = maxTicks;
            iterationTicksKind = barCollection.FirstOrDefault(static b => b.Date.Count > 0)?.Date.First().Kind ?? default;

            int i = 0;
            foreach (var bars in barCollection)
            {
                var dates = bars.Date;
                var count = dates.Count;
                var ticks = longPool.Rent(count);
                for (int j = 0; j < count; j++)
                {
                    ticks[j] = dates[j].Ticks;

                    if (dates[j].Kind != iterationTicksKind)
                    {
                        var msg = $"bar dates must have identical kind; expected: {iterationTicksKind}; bar: {bars}, date: {dates[j]}";
                        throw new ArgumentException(msg, nameof(barCollection));
                    }
                }

                var node = nodes[i] ?? new Node();
                node.iteration = 0;
                node.ticks = ticks;
                node.count = count;
                nodes[i] = node;

                if (count > 0 && ticks[0] < iterationTicks)
                {
                    iterationTicks = ticks[0];
                }

                i++;
            }

            for (i = 0; i < barsCount; i++)
            {
                var node = nodes[i];
                var next = i < barsCount - 1 ? nodes[i + 1] : null;
                var prev = i > 0 ? nodes[i - 1] : null;

                node.next = next;
                node.prev = prev;
            }

            for (i = 0; i < barsCount; i++)
            {
                var node = nodes[i];
                var ticks = node.ticks;
                var count = node.count;

                if (count > 0 && ticks[0] == iterationTicks)
                {
                    node.iteration = 1;
                }
                else
                {
                    node.iteration = 0;
                }
            }

            barsMap = CreateBarsMap(barCollection, nodes);

            seek = nodes[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IReadOnlyDictionary<Bars, Node> CreateBarsMap(ICollection<Bars> barCollection, Node[] nodes)
        {
            try
            {
                return barCollection is IReadOnlyList<Bars> barList //its actually List
                    ? new LiteDictionary<Bars, Node>(barList, nodes)
                    : new LiteDictionary<Bars, Node>(barCollection.ToArray(), nodes);
            }
            catch   //duplicate hash codes
            {
                return barCollection
                    .Zip(nodes, static (bars, node) => (bars, node))
                    .ToDictionary(static x => x.bars, static x => x.node);
            }
        }

        public bool Next()
        {
            iterationTicks = maxTicks;
            bool isCompleted = true;

            var node = seek;
            do
            {
                var iter = node.iteration;
                var ticks = node.ticks;
                var count = node.count;

                if (iter > 0)
                {
                    while (iter < count && ticks[iter - 1] == ticks[iter])  //skip duplicate dates
                    {
                        node.iteration = ++iter;
                    }
                }

                if (iter < count)
                {
                    isCompleted = false;
                }
                else //remove completed
                {
                    longPool.Return(ticks, true);
                    node.ticks = default;
                    node.count = default;

                    if (ReferenceEquals(node, seek))
                    {
                        seek = node.next;
                    }
                    if (node.prev != null)
                    {
                        node.prev.next = node.next;
                    }
                    if (node.next != null)
                    {
                        node.next.prev = node.prev;
                    }

                    node = node.next;
                    continue;
                }

                var nextTick = ticks[iter];
                if (nextTick < iterationTicks)
                {
                    iterationTicks = nextTick;
                }

                node = node.next;
            }
            while (node != null);

            if (isCompleted)
            {
                for (int i = 0; i < barsCount; i++)
                {
                    var ticks = nodes[i].ticks;
                    if (ticks != null)
                    {
                        longPool.Return(ticks, true);
                    }
                }

                //nodePool.Return(nodes, false);

                return false;
            }

            node = seek;
            do
            {
                var iter = node.iteration;
                var ticks = node.ticks;

                if (ticks[iter] == iterationTicks)
                {
                    node.iteration = ++iter;
                }

                node = node.next;
            }
            while (node != null);

            return true;
        }

        ~SynchronizedBarIterator()
        {
            //если будет использоваться после итерирования
            nodePool.Return(nodes, false);
        }
    }
}
