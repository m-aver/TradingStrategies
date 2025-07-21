using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingStrategies.Backtesting.Utility
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<(T, int)> WithIndex<T>(this IEnumerable<T> source) => source.Select(static (x, i) => (x, i));
        public static IEnumerable<T> Take<T>(this IEnumerable<T> source, int start, int end) => source.Skip(start).Take(end - start);
        public static double MaxOrNaN<T>(this IEnumerable<T> source, Func<T, double> selector) => source.Any() ? source.Max(selector) : double.NaN; 

        //копирует элементы исходной коллекции в переданный буффер и возвращает итератор по этому буфферу в пределах исходной коллекции
        //при переполнении буфера заполняет его последними элементами коллекции, возвращает итератор по начальной части исходной коллекции, а затем по буфферу
        public static IEnumerable<T> ToBuffer<T>(this IEnumerable<T> source, T[] buffer)
        {
            int bufferLength = buffer.Length;
            int sourceLength = 0;

            if (bufferLength == 0)
            {
                return source;
            }

            int i = 0;
            foreach (var item in source)
            {
                if (i == bufferLength)
                {
                    sourceLength += i;
                    i = 0;
                }

                buffer[i++] = item;
            }
            sourceLength += i;

            if (sourceLength <= bufferLength)
            {
                return new ArraySegment<T>(buffer, 0, i); //быстрее чем Take или yield
            }
            else
            {
                return source
                    .Take(sourceLength - bufferLength)
                    .Concat(new ArraySegment<T>(buffer, i, bufferLength - i))
                    .Concat(new ArraySegment<T>(buffer, 0, i));
            }
        }
    }
}
