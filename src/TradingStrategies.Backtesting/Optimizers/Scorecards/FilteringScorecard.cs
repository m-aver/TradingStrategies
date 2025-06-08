using System.Windows.Forms;
using WealthLab;

//вспомогательный скорекард
//позволяет фильтровать результаты для упрощения ориентирования по таблице

//WARN: не совместим c Exhaustive оптимизером
//кажется проблема в контролах 1D/2D графиков

namespace TradingStrategies.Backtesting.Optimizers.Scorecards
{
    internal class FilteringScorecard : BasicExScorecard
    {
        public new const string DisplayName = "Filtering Scorecard";
        public override string FriendlyName => DisplayName;

        private static bool FilterPerfomance(SystemPerformance performance)
        {
            return
                performance is null ||
                performance.Results.Positions.Count < 100 ||
                performance.Results.NetProfit <= performance.Strategy.StartingEquity;
        }

        private bool FilterResults(ListViewItem resultRow)
        {
            var sharpe = TryGetNumericalIndicator(resultRow, BasicExScorecard.Sharpe);
            var avgMonthReturn = TryGetNumericalIndicator(resultRow, BasicExScorecard.AvgMr);
            var maxDrawdown = TryGetNumericalIndicator(resultRow, BasicExScorecard.MaxDrawdownPercent);
            var longestDrawdown = TryGetNumericalIndicator(resultRow, BasicExScorecard.LongestDrawdownInDays);

            return
                sharpe < 1 ||
                avgMonthReturn < 8 ||
                maxDrawdown > 50 ||
                longestDrawdown > 300;
        }

        //просто вернуться из метода не вариант
        //в таком случае добавится пустая строка с параметрами оптимизации и это будет мешать сортировке
        public override void PopulateScorecard(ListViewItem resultRow, SystemPerformance performance)
        {
            if (FilterPerfomance(performance))
            {
                RemoveRow(resultRow);
            }
            else
            {
                base.PopulateScorecard(resultRow, performance);

                if (FilterResults(resultRow))
                {
                    RemoveRow(resultRow);
                }
            }
        }

        private void RemoveRow(ListViewItem resultRow)
        {
            //если ListView не привязан, то выбрасывается NRE и вызывающий код должен обработать/проигнорировать строку
            //актуально в ParallelExhaustive оптимизерах

            var listView = resultRow.ListView;

            if (listView.InvokeRequired)
            {
                listView.Invoke(
                    static (ListView view, ListViewItem row) => view.Items.Remove(row),
                    listView, resultRow);
            }
            else
            {
                listView.Items.Remove(resultRow);
            }
        }

        private double? TryGetNumericalIndicator(ListViewItem resultRow, string indicatorName)
        {
            var columns = base.ColumnHeadersRawProfit;
            var items = resultRow.SubItems;

            //поиск с конца, т.к. WealthLab добавляет в начало строки параметры стратегии
            var indicatorIndex = columns.Count - 1 - columns.IndexOf(indicatorName);
            var rowIndex = items.Count - 1 - indicatorIndex;

            var rawResult = resultRow.SubItems[rowIndex];

            return double.TryParse(rawResult.Text, out var result) ? result : null;
        }
    }
}
