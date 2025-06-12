using System.Reflection;
using WealthLab;
using TradingStrategies.Utilities.InternalsProxy;

namespace TradingStrategies.Backtesting.Optimizers.Own;

//private members access
public static class TradingSystemExecutorExtensions
{
    private static readonly Type TseType = typeof(TradingSystemExecutor);

    private static readonly BindingFlags PrivateFlags = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly FieldInfo posSizer_Field = TseType.GetField("posSizer_0", PrivateFlags);
    private static readonly FieldInfo _barsBeingProcessed_Field = TseType.GetField("bars_0", PrivateFlags);
    private static readonly FieldInfo _wealthScriptExecuting_Field = TseType.GetField("wealthScript_0", PrivateFlags);
    private static readonly FieldInfo _rawProfitMode_Field = TseType.GetField("bool_16", PrivateFlags);
    private static readonly FieldInfo position_0_Field = TseType.GetField("position_0", PrivateFlags);
    private static readonly FieldInfo _barsSet_Field = TseType.GetField("ilist_0", PrivateFlags);

    private static readonly FieldInfo positionSize_Field = TseType.GetField("positionSize", BindingFlags.NonPublic | BindingFlags.Static);
    public static PositionSize positionSize { get => (PositionSize) positionSize_Field.GetValue(null); }

    extension(TradingSystemExecutor tse)
    {
        public Bars _barsBeingProcessed { get => tse.BarsBeingProcessed; set => _barsBeingProcessed_Field.SetValue(tse, value); }
        public WealthScript _wealthScriptExecuting {get => tse.WealthScriptExecuting; set => _wealthScriptExecuting_Field.SetValue(tse, value);}

        public PosSizer posSizer {get => tse.PosSizer; set => posSizer_Field.SetValue(tse, value);}

        public Position position_0 {get => (Position) position_0_Field.GetValue(tse); set => position_0_Field.SetValue(tse, value);}
        public bool _rawProfitMode {get => (bool) _rawProfitMode_Field.GetValue(tse); set => _rawProfitMode_Field.SetValue(tse, value);}
        public IList<Bars> _barsSet {get => (IList<Bars>) _barsSet_Field.GetValue(tse); set => _barsSet_Field.SetValue(tse, value);}
    }
}
