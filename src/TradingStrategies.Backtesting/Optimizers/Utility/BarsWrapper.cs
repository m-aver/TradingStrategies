using System.Reflection;
using WealthLab;

//SynchronizedBarIterator активно использует GetHashCode от Bars.UniqueDescription (через словари)
//судя по профайлеру на этом тратится много ресурсов, изначально там формируется большая строка

//также стандартный хеш-код приводит к коллизиям в словарях
//вызывает более дорогие GetHashCode и Equals

namespace TradingStrategies.Backtesting.Optimizers.Utility
{
    internal static class BarsHelper
    {
        public static BarsWrapper WithHash(this Bars bars, int hash)
        {
            var wrapper = new BarsWrapper(hash);
            CloneBars(bars, wrapper);
            wrapper.UniqueDescription = hash.ToString();
            return wrapper;
        }

        private static void CloneBars(Bars source, Bars target)
        {
            var fields = typeof(Bars)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                var value = field.GetValue(source);
                field.SetValue(target, value);
            }
        }
    }

    internal class BarsWrapper : Bars
    {
        private readonly int hash;

        public BarsWrapper(int hash)
        {
            this.hash = hash;
        }

        public new string UniqueDescription
        {
            get => base.UniqueDescription;
            set
            {
                typeof(Bars)
                    .GetField("_uniqueDesc", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(this, value);
            }
        }

        public override int GetHashCode()
        {
            return hash;
        }

        public override bool Equals(object obj)
        {
            return
                obj is BarsWrapper bars &&
                bars.hash == this.hash;
        }
    }
}