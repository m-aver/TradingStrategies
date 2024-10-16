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
        private DateTime iterationDate;
        private readonly Dictionary<Bars, int> barsMap;
        private readonly Bars[] barsCollection;
        private readonly int[] iterations;
        private int startIdx = 0;
        private readonly int barsCount;

        private readonly ArrayPool<int> intPool;
        private readonly ArrayPool<Bars> barsPool;

        //дата текущей итерации
        //соответствует дате текущего бара одной (или нескольких) серии,
        //либо лежит между текущей и следующей итерацией остальных серий
        //если они уже начали и еще не закончили итерирование
        public DateTime Date => iterationDate;

        //номер бара на текущей итерации данной серии
        //-1 если серия еще не начала итерироваться (лежит в будущем)
        //или номер последнего бара если уже закончила итерирование (осталась в прошлом)
        public int Bar(Bars bars) => barsMap[bars] - 1;

        public SynchronizedBarIterator(ICollection<Bars> barCollection)
        {
            barsCount = barCollection.Count;
            
            intPool = ArrayPool<int>.Shared;
            barsPool = ArrayPool<Bars>.Shared;

            barsMap = new Dictionary<Bars, int>(barsCount);
            iterations = intPool.Rent(barsCount);
            barsCollection = barsPool.Rent(barsCount);
            barCollection.CopyTo(barsCollection, 0);

            iterationDate = DateTime.MaxValue;

            for (int i = 0; i < barsCount; i++)
            {
                Bars item = barsCollection[i];
                var startDate = item.Date[0];
                if (item.Count > 0 && startDate < iterationDate)
                {
                    iterationDate = startDate;
                }
            }

            for (int i = 0; i < barsCount; i++)
            {
                Bars item = barsCollection[i];
                if (item.Count > 0 && item.Date[0] == iterationDate)
                {
                    barsMap[item] = 1;
                    iterations[i] = 1;
                }
                else
                {
                    barsMap[item] = 0;
                    iterations[i] = 0;
                }
            }
        }

        public bool Next()
        {
            iterationDate = DateTime.MaxValue;
            bool isCompleted = true;
            int toRemove = -1;

            for (int i = startIdx; i < barsCount; i++)
            {
                Bars item = barsCollection[i];
                int num = iterations[i];

                if (num >= 1)
                {
                    while (num < item.Count && item.Date[num - 1] == item.Date[num])
                    {
                        num++;
                        barsMap[item] = num;
                        iterations[i] = num;
                    }
                }

                if (num < item.Count)
                {
                    isCompleted = false;
                }
                else
                {
                    toRemove = i;
                    continue;
                }

                var next = item.Date[num];
                if (next < iterationDate)
                {
                    iterationDate = next;
                }
            }

            if (isCompleted)
            {
                barsPool.Return(barsCollection, true);
                intPool.Return(iterations, true);
                return false;
            }
            if (toRemove >= 0)
            {
                var index = startIdx;
                var length = toRemove - index;
                var destIndex = index + 1;

                Array.Copy(barsCollection, index, barsCollection, destIndex, length);
                Array.Copy(iterations, index, iterations, destIndex, length);

                startIdx++;
            }

            for (int i = startIdx; i < barsCount; i++)
            {
                Bars item = barsCollection[i];
                int num = iterations[i];

                if (item.Date[num] == iterationDate)
                {
                    num++;
                    barsMap[item] = num;
                    iterations[i] = num;
                }
            }

            return true;
        }
    }
}
