using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class WealthScriptExtensions
{
    extension(WealthScript wealthScript)
    {
        public void Execute(
            Bars bars,
            ChartRenderer? renderer,
            TradingSystemExecutor executor,
            DataSource dataSource)
        {
            wealthScript.method_4(
                bars, 
                renderer, 
                executor, 
                dataSource);
        }
    }
}
