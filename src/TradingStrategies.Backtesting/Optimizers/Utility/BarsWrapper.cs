using System.Reflection;
using TradingStrategies.Backtesting.Utility;
using WealthLab;

//SynchronizedBarIterator активно использует GetHashCode от Bars.UniqueDescription (через словари)
//судя по профайлеру на этом тратится много ресурсов, изначально там формируется большая строка

//также стандартный хеш-код приводит к коллизиям в словарях
//вызывает более дорогие GetHashCode и Equals
//выгодно использовать уникальные хеш коды, лежащие в непрерывном диапазоне, так чтобы mod гарантированно не выдавал коллизий

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

        //нужна полная копия, т.к. DataSeries имеет внутреннее кеширование без поддержки конкурентности
        //это ведет к проблем, например к бесконечному циклу при поиске бакетов в словаре
        public static Bars DeepClone(this Bars source) => CloneUtil<Bars>.DeepClone(source);

        public static Bars Prepare(this Bars bars, int hash) => bars.WithHash(hash).DeepClone();
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