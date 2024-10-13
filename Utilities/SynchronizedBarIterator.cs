using System;
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
        private DateTime dateTime_0;
        private readonly ICollection<Bars> icollection_0;
        private readonly Dictionary<string, int> dictionary_0 = new Dictionary<string, int>();

        //не нужно считать хэш строк
        private readonly Dictionary<Bars, int> dictionary_1;
        private readonly List<Bars> barsCollection;
        private readonly List<int> iterations;

        //дата текущей итерации
        //соответствует дате текущего бара одной (или нескольких) серии,
        //либо лежит между текущей и следующей итерацией остальных серий
        //если они уже начали и еще не закончили итерирование
        public DateTime Date => dateTime_0;

        //номер бара на текущей итерации данной серии
        //-1 если серия еще не начала итерироваться (лежит в будущем)
        //или номер последнего бара если уже закончила итерирование (осталась в прошлом)
        public int Bar(Bars bars) => dictionary_1[bars] - 1;

        public SynchronizedBarIterator(ICollection<Bars> barCollection)
        {
            //ищем дату первого бара из всего датасета
            //и выставляем num 0 (разрешаем дальшейнее итерирование) на сериях которые начинаются с этой даты

            dictionary_1 = new Dictionary<Bars, int>(barCollection.Count);
            iterations = new List<int>(barCollection.Count);
            barsCollection = barCollection.ToList();
            icollection_0 = barCollection;

            dateTime_0 = DateTime.MaxValue;

            for (int i = 0; i < barsCollection.Count; i++)
            {
                Bars item = barsCollection[i];
                var startDate = item.Date[0];
                if (item.Count > 0 && startDate < dateTime_0)
                {
                    dateTime_0 = startDate;
                }
            }

            for (int i = 0; i < barsCollection.Count; i++)
            {
                Bars item = barsCollection[i];
                if (item.Count > 0 && item.Date[0] == dateTime_0)
                {
                    dictionary_1[item] = 1;
                    iterations.Add(1);
                }
                else
                {
                    dictionary_1[item] = 0;
                    iterations.Add(0);
                }
            }
        }

        public bool Next()
        {
            dateTime_0 = DateTime.MaxValue;
            bool flag = true;
            int toRemove = -1;
            DateTime next = dateTime_0;

            for (int i = 0; i < barsCollection.Count; i++)
            {
                Bars item = barsCollection[i];
                int num = iterations[i];

                if (num >= 1)
                {
                    while (num < item.Count && item.Date[num - 1] == item.Date[num])
                    {
                        num++;
                        dictionary_1[item] = num;
                        iterations[i] = num;
                    }
                }

                if (num < item.Count)
                {
                    flag = false;
                }
                else
                {
                    toRemove = i;
                    continue;
                }

                next = item.Date[num];
                if (next < dateTime_0)
                {
                    dateTime_0 = next;
                }
            }

            if (flag)
            {
                return false;
            }
            if (toRemove >= 0)
            {
                barsCollection.RemoveAt(toRemove);
                iterations.RemoveAt(toRemove);
            }

            //пытаемся итерировать каждую серию, если наткнулись на наименьшую следующую дату то фиксируем итерацию серии
            for (int i = 0; i < barsCollection.Count; i++)
            {
                Bars item = barsCollection[i];
                int num = iterations[i];

                if (item.Date[num] == dateTime_0)
                {
                    num++;
                    dictionary_1[item] = num;
                    iterations[i] = num;
                }
            }

            return true;
        }



        public bool NextOld()
        {
            //это видимо чтобы проскипать возможные бары с одинаковой датой (в рамках одной серии)
            foreach (Bars item in icollection_0)
            {
                int num = dictionary_0[item.UniqueDescription];
                if (num >= 0)
                {
                    while (num < item.Count - 1 && item.Date[num] == item.Date[num + 1])
                    {
                        num++;
                        dictionary_0[item.UniqueDescription] = num;
                    }
                }
            }

            //продолжаем итерирование если хотя бы одна серия еще не дошла до конца
            bool flag = true;
            foreach (Bars item2 in icollection_0)
            {
                if (dictionary_0[item2.UniqueDescription] < item2.Count - 1)
                {
                    flag = false;
                    break;
                }
            }
            if (flag)
            {
                return false;
            }

            //ищем наименьшую дату следующего итерируемого бара среди всех серий
            dateTime_0 = DateTime.MaxValue;
            foreach (Bars item3 in icollection_0)
            {
                int num2 = dictionary_0[item3.UniqueDescription];
                if (num2 < item3.Count - 1 && item3.Date[num2 + 1] < dateTime_0)
                {
                    dateTime_0 = item3.Date[num2 + 1];
                }
            }

            //пытаемся итерировать каждую серию, если наткнулись на наименьшую следующую дату то фиксируем итерацию серии
            foreach (Bars item4 in icollection_0)
            {
                int num3 = dictionary_0[item4.UniqueDescription];
                if (num3 < item4.Count - 1)
                {
                    num3++;
                    if (item4.Date[num3] == dateTime_0)
                    {
                        dictionary_0[item4.UniqueDescription] = num3;
                    }
                }
            }

            return true;
        }
    }
}
