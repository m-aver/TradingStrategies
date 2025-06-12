using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class TradingSystemExecutorExtensions
{
    extension(TradingSystemExecutor executor)
    {
        public double RiskStopLevel { get => executor.RiskStopLevel; set => executor.RiskStopLevel = value; }
        public List<Position> CurrentPositions { get => executor.CurrentPositions; }
        public List<Alert> CurrentAlerts { get => executor.CurrentAlerts; }
        public List<Position> ActivePositions { get => executor.ActivePositions; }
        public double AutoProfitLevel { get => executor.AutoProfitLevel; set => executor.AutoProfitLevel = value; }
        public List<Alert> MasterAlerts { get => executor.MasterAlerts;  }
        public PosSizer PosSizer { get => executor.PosSizer; }
    }
}
