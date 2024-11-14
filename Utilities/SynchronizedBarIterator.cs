using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WealthLab;

//TODO:
//как будто ноды сильно грузят GC, пул не помогает?
//какой то этот аррэй пул проблемный, надо либо свой писать, либо что-то другое использовать
//?: 1 - слишком много аллокаций, 2 - локи и бездействие цп

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
        private readonly Dictionary<Bars, Node> barsMap;
        private readonly int barsCount;

        private Node seek;
        private readonly Node[] nodes;
        private readonly ArrayPool<Node> nodePool;
        private readonly ArrayPool<long> longPool;

        //дата текущей итерации
        //соответствует дате текущего бара одной (или нескольких) серии,
        //либо лежит между текущей и следующей итерацией остальных серий
        //если они уже начали и еще не закончили итерирование
        public DateTime Date => new DateTime(iterationTicks);

        //номер бара на текущей итерации данной серии
        //-1 если серия еще не начала итерироваться (лежит в будущем)
        //или номер последнего бара если уже закончила итерирование (осталась в прошлом)
        //(внутренние итерации смещены на +1 для оптимизации)
        public int Bar(Bars bars) => barsMap[bars].iteration - 1;

        public SynchronizedBarIterator(ICollection<Bars> barCollection)
        {
            barsCount = barCollection.Count;
            barsMap = new Dictionary<Bars, Node>(barsCount);

            longPool = ArrayPool<long>.Shared;
            nodePool = ArrayPool<Node>.Shared;

            nodes = nodePool.Rent(barsCount);

            iterationTicks = long.MaxValue;

            int i = 0;
            foreach(var bars in barCollection)
            {
                var dates = bars.Date;
                var count = dates.Count;
                var ticks = longPool.Rent(count);
                for (int j = 0; j < count; j++)
                {
                    ticks[j] = dates[j].Ticks;
                }

                var node = nodes[i] ?? new Node();
                node.iteration = 0;
                node.ticks = ticks;
                node.count = count;
                nodes[i] = node;

                barsMap[bars] = node;

                var startTicks = ticks[0];
                if (count > 0 && startTicks < iterationTicks)
                {
                    iterationTicks = startTicks;
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

            seek = nodes[0];
        }

        public bool Next()
        {
            iterationTicks = long.MaxValue;
            bool isCompleted = true;

            var node = seek;
            do
            {
                var iter = node.iteration;
                var ticks = node.ticks;
                var count = node.count;
                var next = node.next;

                if (iter >= 1)
                {
                    while (iter < count && ticks[iter - 1] == ticks[iter])
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
                        seek = next;
                    }
                    else
                    {
                        node.prev.next = next;

                        if (next != null)
                        {
                            next.prev = node.prev;
                        }
                    }

                    node = next;
                    continue;
                }

                var nextTick = ticks[iter];
                if (nextTick < iterationTicks)
                {
                    iterationTicks = nextTick;
                }

                node = next;
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
            //может быть использовано после итерирования
            nodePool.Return(nodes, false);
        }
    }
}
