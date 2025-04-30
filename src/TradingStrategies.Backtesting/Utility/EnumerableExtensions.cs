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
    }
}
