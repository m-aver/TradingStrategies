using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WealthLab;

//синхронизирует итерацию нескольких серий баров
//таким образом что пересекающиеся по времени серии итерируются параллельно друг другу

namespace TradingStrategies.Utilities
{
    /// <summary>
    /// Optimized version of WealthLab.SynchronizedBarIterator.
    /// Replace it in WealthLab.dll source code using ildasm.exe/ilasm.exe
    /// </summary>
    public class SynchronizedBarIterator
    {
        private long iterationTicks;
        private readonly Dictionary<Bars, int> barsMap;
        private readonly Bars[] barsCollection;
        private readonly int[] iterations;
        private readonly (long[] ticks, int count)[] barsTicks;
        private int startIdx = 0;
        private readonly int barsCount;

        private readonly ArrayPool<int> intPool;
        private readonly ArrayPool<Bars> barsPool;
        private readonly ArrayPool<long> longPool;
        private readonly ArrayPool<(long[], int)> long2dPool;

        //дата текущей итерации
        //соответствует дате текущего бара одной (или нескольких) серии,
        //либо лежит между текущей и следующей итерацией остальных серий
        //если они уже начали и еще не закончили итерирование
        public DateTime Date => new DateTime(iterationTicks);

        //номер бара на текущей итерации данной серии
        //-1 если серия еще не начала итерироваться (лежит в будущем)
        //или номер последнего бара если уже закончила итерирование (осталась в прошлом)
        //(внутренние итерации смещены на +1 для оптимизации)
        public int Bar(Bars bars) => iterations[barsMap[bars]] - 1;

        public SynchronizedBarIterator(ICollection<Bars> barCollection)
        {
            barsCount = barCollection.Count;
            
            intPool = ArrayPool<int>.Shared;
            barsPool = ArrayPool<Bars>.Shared;
            longPool = ArrayPool<long>.Shared;
            long2dPool = ArrayPool<(long[], int)>.Shared;

            barsMap = new Dictionary<Bars, int>(barsCount);
            iterations = intPool.Rent(barsCount);
            barsCollection = barsPool.Rent(barsCount);
            barCollection.CopyTo(barsCollection, 0);
            barsTicks = long2dPool.Rent(barsCount);

            iterationTicks = long.MaxValue;

            for (int i = 0; i < barsCount; i++)
            {
                var dates = barsCollection[i].Date;
                var count = dates.Count;
                var ticks = longPool.Rent(count);
                for (int j = 0; j < count; j++)
                {
                    ticks[j] = dates[j].Ticks;
                }
                barsTicks[i] = (ticks, count);

                var startTicks = ticks[0];
                if (count > 0 && startTicks < iterationTicks)
                {
                    iterationTicks = startTicks;
                }
            }

            for (int i = 0; i < barsCount; i++)
            {
                var bars = barsCollection[i];
                var (ticks, count) = barsTicks[i];
                if (count > 0 && ticks[0] == iterationTicks)
                {
                    iterations[i] = 1;
                }
                else
                {
                    iterations[i] = 0;
                }
                barsMap[bars] = i;
            }
        }

        public bool Next()
        {
            iterationTicks = long.MaxValue;
            bool isCompleted = true;
            int toRemove = -1;

            for (int i = startIdx; i < barsCount; i++)
            {
                var iter = iterations[i];
                var (ticks, count) = barsTicks[i];

                if (iter >= 1)
                {
                    while (iter < count && ticks[iter - 1] == ticks[iter])
                    {
                        iterations[i] = ++iter;
                    }
                }

                if (iter < count)
                {
                    isCompleted = false;
                }
                else
                {
                    toRemove = i;
                    continue;
                }

                var next = ticks[iter];
                if (next < iterationTicks)
                {
                    iterationTicks = next;
                }
            }

            if (isCompleted)
            {
                barsPool.Return(barsCollection, true);
                //intPool.Return(iterations, true);

                foreach (var ticks in barsTicks.Where(x => x.ticks != null))
                {
                    longPool.Return(ticks.ticks, true);
                }
                long2dPool.Return(barsTicks, true);

                return false;
            }

            if (toRemove >= 0)
            {
                longPool.Return(barsTicks[toRemove].ticks, true);
                barsTicks[toRemove] = default;

                if (toRemove != startIdx)
                {
                    var removingBars = barsCollection[toRemove];
                    barsMap[removingBars] = startIdx;
                    for (var i = startIdx; i < toRemove; i++)
                    {
                        var movingBars = barsCollection[i];
                        barsMap[movingBars] = i + 1;
                    }

                    ArrayHelper.ShiftItem(barsCollection, toRemove, startIdx);
                    ArrayHelper.ShiftItem(iterations, toRemove, startIdx);
                    ArrayHelper.ShiftItem(barsTicks, toRemove, startIdx);
                }

                startIdx++;
            }

            for (int i = startIdx; i < barsCount; i++)
            {
                var iter = iterations[i];
                var ticks = barsTicks[i].ticks;

                if (ticks[iter] == iterationTicks)
                {
                    iterations[i] = ++iter;
                }
            }

            return true;
        }

        ~SynchronizedBarIterator()
        {
            //может быть использовано после итерирования
            intPool.Return(iterations, true);
        }
    }
}
