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
        private ICollection<Bars> icollection_0;
        private Dictionary<string, int> dictionary_0 = new Dictionary<string, int>();

        //дата текущей итерации
        //соответствует дате текущего бара одной (или нескольких) серии,
        //либо лежит между текущей и следующей итерацией остальных серий
        //если они уже начали и еще не закончили итерирование
        public DateTime Date => dateTime_0;

        //номер бара на текущей итерации данной серии
        //-1 если серия еще не начала итерироваться (лежит в будущем)
        //или номер последнего бара если уже закончила итерирование (осталась в прошлом)
        public int Bar(Bars bars) => dictionary_0[bars.UniqueDescription];

        public SynchronizedBarIterator(ICollection<Bars> barCollection)
        {
            //ищем дату первого бара из всего датасета
            //и выставляем num 0 (разрешаем дальшейнее итерирование) на сериях которые начинаются с этой даты

            icollection_0 = barCollection;
            foreach (Bars item in barCollection)
            {
                dictionary_0[item.UniqueDescription] = -1;
            }

            dateTime_0 = DateTime.MaxValue;
            foreach (Bars item2 in barCollection)
            {
                if (item2.Count > 0 && item2.Date[0] < dateTime_0)
                {
                    dateTime_0 = item2.Date[0];
                }
            }

            foreach (Bars item3 in barCollection)
            {
                if (item3.Count > 0 && item3.Date[0] == dateTime_0)
                {
                    dictionary_0[item3.UniqueDescription] = 0;
                }
            }
        }

        public bool Next()
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
