using System;
using System.Reflection;

namespace TradingStrategies.Backtesting.Utility
{
    public static class CloneUtil<T>
    {
        private static readonly Func<T, object> clone;

        static CloneUtil()
        {
            var cloneMethod = typeof(T).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            clone = (Func<T, object>)cloneMethod.CreateDelegate(typeof(Func<T, object>));
        }

        public static T ShallowClone(T obj) => (T)clone(obj);
    }
}
