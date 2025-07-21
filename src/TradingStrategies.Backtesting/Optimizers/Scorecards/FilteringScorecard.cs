using System.Windows.Forms;
using WealthLab;

//вспомогательный скорекард
//позволяет фильтровать результаты для упрощения ориентирования по таблице

//WARN: не совместим c Exhaustive оптимизером
//кажется проблема в контролах 1D/2D графиков

namespace TradingStrategies.Backtesting.Optimizers.Scorecards
{
    internal class ResultsFilteredExсeption() : Exception(msg)
    {
        const string msg = $"Results was filtered by {nameof(FilteringScorecard)}";
    }

    internal class FilteringScorecard : CustomScorecard
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
            var sharpe = TryGetNumericalIndicator(resultRow, CustomScorecard.Sharpe);
            var avgMonthReturn = TryGetNumericalIndicator(resultRow, CustomScorecard.AvgMr);
            var maxDrawdown = TryGetNumericalIndicator(resultRow, CustomScorecard.MaxDrawdownPercent);
            var longestDrawdown = TryGetNumericalIndicator(resultRow, CustomScorecard.LongestDrawdownInDays);

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
            var listView = resultRow.ListView;

            //если ListView не привязан выбрасываем эксепшен, актуально в ParallelExhaustive оптимизерах
            if (listView is null)
            {
                throw new ResultsFilteredExсeption();
            }

            //удаляем строку из списка, актуально для встроенных оптимизеров
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
