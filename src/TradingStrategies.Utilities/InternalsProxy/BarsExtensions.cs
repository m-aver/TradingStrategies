using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class BarsExtensions
{
    //public static object GetDivTag(this Bars bars) => bars.DivTag;
    //public static void SetDivTag(this Bars bars, object divTag) => bars.DivTag = divTag;

    extension(Bars bars)
    {
        public object DivTag
        {
            get => bars.DivTag;
            set => bars.DivTag = value;
        }

        public void Block() => bars.method_1();
        public void Unblock() => bars.method_2();
    }
}
