using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class PosSizerExstensions
{
    //public static void SetActivePositions(this PosSizer posSizer, List<Position> positions) => posSizer.ActivePositions = positions;
    //public static void SetPositions(this PosSizer posSizer, List<Position> positions) => posSizer.Positions = positions;
    //public static void SetClosedPositions(this PosSizer posSizer, List<Position> positions) => posSizer.ClosedPositions = positions;

    extension(PosSizer posSizer)
    {
        public List<Position> ActivePositionsProxy { get => posSizer.ActivePositions; set => posSizer.ActivePositions = value; }
        public List<Position> PositionsProxy { get => posSizer.Positions; set => posSizer.Positions = value; }
        public List<Position> ClosedPositionsProxy { get => posSizer.ClosedPositions; set => posSizer.ClosedPositions = value; }

        public List<Position> CandidatesProxy { get => posSizer.Candidates; set => posSizer.Candidates = value; }

        public void PreInitialize(
            TradingSystemExecutor tradingSystemExecutor,
            List<Position> currentPositions,
            List<Position> positions,
            List<Position> closedPositions,
            DataSeries equitySeries,
            DataSeries cashSeries,
            DataSeries drawdownSeries,
            DataSeries drawdownPercentSeries)
        {
            posSizer.method_0(
                tradingSystemExecutor, 
                currentPositions, 
                positions, 
                closedPositions, 
                equitySeries, 
                cashSeries, 
                drawdownSeries, 
                drawdownPercentSeries);
        }
    }
}
