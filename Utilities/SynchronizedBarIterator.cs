using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WealthLab;

namespace TradingStrategies.Utilities
{
    public class SynchronizedBarIterator
    {
        private DateTime dateTime_0;

        private ICollection<Bars> icollection_0;

        private Dictionary<string, int> dictionary_0 = new Dictionary<string, int>();

        public DateTime Date => dateTime_0;

        public SynchronizedBarIterator(ICollection<Bars> barCollection)
        {
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

            dateTime_0 = DateTime.MaxValue;
            foreach (Bars item3 in icollection_0)
            {
                int num2 = dictionary_0[item3.UniqueDescription];
                if (num2 < item3.Count - 1 && item3.Date[num2 + 1] < dateTime_0)
                {
                    dateTime_0 = item3.Date[num2 + 1];
                }
            }

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

        public int Bar(Bars bars)
        {
            return dictionary_0[bars.UniqueDescription];
        }
    }
}
