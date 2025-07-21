using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class BarsExtensions
{
    extension(Bars bars)
    {
        public object DivTag { get => bars.DivTag; set => bars.DivTag = value; }

        public void Block() => bars.method_1();
        public void Unblock() => bars.method_2();
    }
}
