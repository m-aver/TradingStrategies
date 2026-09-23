using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Utility;
using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities.InternalsProxy;
using WealthLab;
using static System.Windows.Forms.ListViewItem;

namespace TradingStrategies.Backtesting.Optimizers.Charts;

//разбивает диапазон исходных результатов на периоды (окна) и подбирает успешные варианты параметров стратегии на этих периодах
//выбранные стратегии затем расчитываются на следующем периоде, который не входит в выборку для тестирования
//таким образом имитируется сценарий когда стратегия меняет (адаптирует) свои параметры во времени по результатам предыдущих периодов работы
//все это работает только для стратегий, которые выставляют позиции пропорционально накопленному капиталу
//адеватными будут только те метрики скорекарда, которые расчитываются исходя из кривой эквити, а не данных самих позиций

//кнопки:
//- Calculate
//расчитывает комбинацию-лидера, у составных систем которой при тестировании были лучшие значения опорной метрики
//выводит инфу о группах отобранных систем для каждого периода и расчитывает общее число комбинаций для понимания нагрузки на оптимизацию
//- Optimize
//расчитывает результаты всех возможных комбинаций отобранных стратегий на разных периодах и выводит их в текстовое окно
//при большом количестве комбинаций расчет может сильно затянуться
//- PreOptimize
//расчитывает приблизительные результаты всех возможных комбинаций отобранных стратегий на разных периодах
//основывается только на разницах доходностей начала и конца каждого периода, не склеивает эквити и не вызывает скорекард
//выдает ограниченный и не точный набор метрик, которые можно высосать из одних только доходностей по периодам
//но работает ощутимо быстрее чем Optimize и позволяет обработать больше комбинаций
//выводит результаты в текстовое окно и csv файл во временной директории для, например, последующего статистического анализа

//доп фичи:
//- MaxGroup
//ограничение максимального числа систем в группе
//чтобы появилась возможность подослабить условия входа для неудачных периодов и при этом не получить дохуя комбинаций от разрастания систем в удачных
//в группах, где отфильтрованных систем получилось больше максимального количества, сортируем и отбираем самые лучшие

//TODO:
//посчитать и вывести результаты системы в скорекарде, отобранной в DoCalc
//надо еще порыться в инете на тему кастомной быстрой реализации расчета логарифма
//позиции тоже надо бы скомбинить, хотя бы примерно
//можно было бы сделать так, чтобы при выборе результата комбинации, запускался бектест (ограниченно)
//  кажется достаточно извлечь коллекцию IPerfomanceVisualizer из формы оптимизера и скормить им SystemPerformance комбинации
//  самое ленивое тут рисовать контролы под каждую комбинацию и нормально заполнить SystemPerformance, не одной лишь EquityCurve
//рассмотреть сэмплирование похожих стратегий в группе
//  + увеличивает производительность, уменьшая размер групп
//  + при ограниченном размере группы, ее всю могут заполнить похожие системы, не дав инсайты по остальному разнообразию
//  - потенциально введение хитрых правил исключения может исказить общую статистику 
//  идеи по реализации сэмплирования
//    т.к. в общем случае не понятно как оценить меру влияния смещения значения параметров на поведение стратегии (например, для периодов МovАvg 1-2 разница сильно значимее чем для 50-51)
//    предлагаю добавить к сравнению результаты стратегии, например разницу в общей доходности или порог корреляции доходностей на периодах
//    например если стратегии имеют близко лежащие параметры и их доходности не сильно отличаются, то сэмплируем их
//    видимо в итоге это будет выглядеть так, что сначала сортируем группу по всем параметрам (OrderBy, ThenBy), а затем проходясь последовательно, запоминаем опорную и фильтруем похожие
//    все это не должно сильно ударить по производительности, т.к. обычно в группе не очень много систем, да и это окупится сокращением числа комбинаций
//надо бы добавить возможность указывать несколько опорных метрик для отбора систем

internal class OptResultsWindowAdapteeOptimizer : UserControl
{
    private OptimizationResultListEx results;
    private StrategyScorecard scorecard;

    private readonly IPeriodSeparator periodSeparator = PeriodSeparatorFactory.Create(PeriodCalcType.Simple);

    public OptResultsWindowAdapteeOptimizer()
    {
        InitializeComponents();
    }

    internal void UpdateResults(OptimizationResultListEx results, StrategyScorecard scorecard)
    {
        this.results = results;
        this.scorecard = scorecard;

        comboMetrics.Items.Clear();
        comboMetrics.Items.AddRange(results.Names.ToArray());
    }

    private List<(OptimizationResultEx result, DateTimeRange range)> windowResults = new(); //избранная комбинация
    private List<(List<OptimizationResultEx> results, DateTimeRange range)> windowResultGroups = new(); //все удовлетворяющие варианты

    //разбиваем исходный диапазон дат на окна с длинной deep и итеративным смещением на step
    //отбираем системы по указанным in и out параметрам на данных диапазонах 
    //выставляем отобранным системам даты следующего окна, на которых не велся отбор
    //будем проверять, как системы, отобранные на прошлом, будут вести себя в будующем
    private void DoCalc()
    {
        var inputData = GetInputData();
        var metricIdx = results.Names.IndexOf(inputData.MetricName);

        var range = results.GetDatesRange();

        var approximatedRangesCount = (int)(range.Offset.Ticks / PeriodSeparatorHelper.GetApproximatedSpan(inputData.PeriodStep).Ticks);

        var systemIdx = new Stack<(int idx, DateTimeRange range, bool isRejected)>(approximatedRangesCount);
        var systemIdxGroups = new Stack<(HashSet<int> idx, DateTimeRange range)>(approximatedRangesCount);

        var windows = GetWindowRanges(range, inputData.PeriodDeep, inputData.PeriodStep);

        foreach (var window in windows)
        {
            //в случае если окна не пересекаются, получим не исходные окна, а состыкованные диапазоны, хотя в таком случае результаты у них могут быть уже другие, но кажется это вырожденный кейс для практики, поэтому похуй, но и пофиксить вроде не сложно
            //можно бы сначала заполнить группы, а потом на их основе выбирать конкретную композицию, но пока вот так

            ShiftResults(window);

            if (systemIdx.Any() &&
                !systemIdx.Peek().isRejected)
            {
                //обновляем диапазон текущего лидера
                var idx = systemIdx.Pop();
                var newRange = new DateTimeRange(idx.range.DateTime, window.EndDateTime);
                idx.range = newRange;

                //проверяем, удовлетворяет ли лидер
                var metrics = results.ResultsEx[idx.idx].Results;

                var isSatisfied =
                    inputData.BetterThanMinValue(metrics[metricIdx]);

                idx.isRejected = !isSatisfied;
                systemIdx.Push(idx);
            }

            //обновляем диапазон последней группы
            if (systemIdxGroups.Any())
            {
                var idxGroup = systemIdxGroups.Pop();
                var newRange = new DateTimeRange(idxGroup.range.DateTime, window.EndDateTime);
                idxGroup.range = newRange;
                systemIdxGroups.Push(idxGroup);
            }

            // если еще ничего не выбрано или выбранное не удовлетворяют условиям - ищем новое

            var satisfied = results.ResultsEx
                .Where(r => inputData.BetterThanMaxValue(r.Results[metricIdx]));

            // отбираем лучшие
            if (inputData.MaxGroup is not null)
            {
                satisfied = satisfied
                    .OrderByDescending(r => r.Results[metricIdx], inputData.MetricComparer)
                    .Take(inputData.MaxGroup.Value);
            }

            //инициализируем диапазон концом текущего периода, т.к. в реальных условиях знаем результаты системы только постфактум
            var initRange = new DateTimeRange(window.EndDateTime, window.EndDateTime);

            //заполняем группу
            var groupIdx = satisfied.Select(r => results.ResultsEx.IndexOf(r)).ToHashSet();
            systemIdxGroups.Push((groupIdx, initRange));

            if (satisfied.Any())
            {
                //поиск лучшего результата из удовлетворяющих
                var best = satisfied.OrderBy(r => r.Results[metricIdx], inputData.MetricComparer).Last();
                var bestIdx = results.ResultsEx.IndexOf(best);

                if (systemIdx.Count == 0 ||
                    systemIdx.Peek().isRejected)
                {
                    systemIdx.Push((bestIdx, initRange, false));
                }
            }
        }

        //удаляем последний диапазон, если он был только проинициализирован, но не реализован
        if (systemIdx.Any() &&
            systemIdx.Peek().range.Offset == TimeSpan.Zero)
        {
            _ = systemIdx.Pop();
        }

        //удаляем последнию группу, т.к. ее уже не на чем реализовывать - конец периода
        if (systemIdxGroups.Any())
        {
            _ = systemIdxGroups.Pop();
        }

        //заполняем результаты
        windowResults.Clear();

        foreach (var sys in systemIdx)
        {
            var result = results.ResultsEx[sys.idx];
            ShiftResults(result, sys.range);
            windowResults.Add((result.Copy(), sys.range));
        }

        //заполняем результаты групп
        windowResultGroups.Clear();

        foreach (var group in systemIdxGroups)
        {
            var resultsGroup = new List<OptimizationResultEx>();

            foreach (var idx in group.idx)
            {
                var result = results.ResultsEx[idx];
                ShiftResults(result, group.range);
                resultsGroup.Add(result.Copy());
            }

            windowResultGroups.Add((resultsGroup, group.range));
        }
    }

    //формирует все возможные комбинации избранных систем, и считает метрики их композиций
    //в целом вроде как плюс минус норм считает, хотя и с погрешностями
    private void DoOptimization()
    {
        this.optimizationResults.Clear();

        var optimizationResults = new ConcurrentBag<(ListViewItem metricRow, List<(OptimizationResultEx result, DateTimeRange range)> results)>();

        Parallel.ForEach(
            GetSerialResults(Environment.ProcessorCount),
            (resultGroupSerial) =>
            {
                foreach (var resultGroup in resultGroupSerial)
                {
                    var equities = resultGroup.Select(g => g.value).Select(r => r.PerformanceShifted.Results.EquityCurve);
                    var combinedEquity = CombineEquities(equities);

                    var performance = new SystemPerformance(null);
                    CopyPerformance(performance, resultGroup.First().value.PerformanceShifted);
                    performance.Results.EquityCurveProxy = combinedEquity;

                    //у скорекарда не должно быть собственного стейта
                    var row = new ListViewItem();
                    scorecard.PopulateScorecard(row, performance);
                    optimizationResults.Add((row, resultGroup.Select(x => (x.value, x.group)).ToList()));
                }
            }
        );

        this.optimizationResults = optimizationResults.ToList();
    }

    //партиционирует комбинации, каждую партицию нужно обрабатывать строго последовательно
    private IEnumerable<IEnumerable<IEnumerable<(DateTimeRange group, OptimizationResultEx value)>>> GetSerialResults(int threadsNum)
    {
        for (int i = 0; i < threadsNum; i++)
        {
            var copiedResultGroups = windowResultGroups
                .Select(x => (results: x.results.ToList(), range: x.range))
                .ToList();

            var resultGroups = copiedResultGroups
                .OrderBy(g => g.range.DateTime)
                .SelectMany(g => g.results.Select(r => (g.range, r)))
                .GroupBy(x => x.range, x => x.r);

            var resultCombinations = CombineGroups(resultGroups);
            resultCombinations = SparseIterations(resultCombinations, i, threadsNum);

            yield return resultCombinations;
        }
    }

    private List<(ListViewItem metricRow, List<(OptimizationResultEx result, DateTimeRange range)> results)> optimizationResults { get; set; } = new();

    //выводит все возможные составные системы и их метрики
    private void ViewOptimizationResults()
    {
        txtResult.Clear();

        const string separator = "\t|\t";
        var culture = CultureInfo.InvariantCulture;

        var metricsRowView = string.Join(separator, ["id", .. scorecard.ColumnHeadersRawProfit]);
        txtResult.AppendText(metricsRowView + Environment.NewLine);

        var dataViewBuilder = new StringBuilder();
        foreach (var (optResult, ix) in optimizationResults.WithIndex(1))
        {
            foreach (var result in optResult.results)
            {
                var infoView = GetResultView(result);
                dataViewBuilder.AppendLine(infoView);
            }

            var metrics = optResult.metricRow.SubItems.OfType<ListViewSubItem>()
                .Select(x => x.Text).Where(x => !string.IsNullOrWhiteSpace(x)).Prepend(ix.ToString());
            var rowView = string.Join(separator, metrics);
            dataViewBuilder.AppendLine(rowView);
        }

        txtResult.AppendText(dataViewBuilder.ToString());

        CopyOptimizationResultsToClipboard();
    }

    private void CopyOptimizationResultsToClipboard()
    {
        const string separator = ";";

        var txtResult = new StringBuilder();

        var metricsRowView = string.Join(separator, ["id", .. scorecard.ColumnHeadersRawProfit]);
        txtResult.AppendLine(metricsRowView);

        var dataViewBuilder = new StringBuilder();
        foreach (var (optResult, ix) in optimizationResults.WithIndex(1))
        {
            var metrics = optResult.metricRow.SubItems.OfType<ListViewSubItem>()
                .Select(x => x.Text).Where(x => !string.IsNullOrWhiteSpace(x)).Prepend(ix.ToString());
            var rowView = string.Join(separator, metrics);
            dataViewBuilder.AppendLine(rowView);
        }

        txtResult.Append(dataViewBuilder.ToString());

        Clipboard.SetText(txtResult.ToString());
    }

    //объединяет эквити из разных диапазонов в один
    //требует на вход упорядоченные, не пересекающиеся диапазоны
    //использует идею линейного сложения кривых эквити в логарифмическом пространстве - величины доходностей должны сохраняться
    //можно использовать только если исходная стратегия открывает позиции пропорционально накопленному капиталу
    //иначе нужно билдить комбинированную стратегию и запускать ее явно на бэктест, что затратнее и пока не понятно как нормально реализовать
    //здесь будет важен перфоманс, нужна поддержка параллельных запусков (возможно идея использовать логарифмы не самая удачная)
    private static DataSeries CombineEquities(IEnumerable<DataSeries> equities)
    {
        return CombineEquities(equities.Select(e => e.ToPoints())).ToSeries("combined-equity");
    }

    private static IEnumerable<DataSeriesPoint> CombineEquities(IEnumerable<IEnumerable<DataSeriesPoint>> equities)
    {
        if (!equities.Any())
        {
            yield break;
        }

        equities = equities.Where(e => e.Any());

        equities = equities.Select(e => e.Select(p => p.Transform(Math.Log)));

        var prev = equities.First().First();

        foreach (var equity in equities)
        {
            var delta = equity.First() - prev;

            foreach (var point in equity)
            {
                if (prev.Date > point.Date)
                {
                    throw new InvalidOperationException($"Passed equities should be ordered and not overlaped, got current point: {point} and previous point: {prev}");
                }

                prev = point - delta;

                yield return prev.Transform(Math.Exp);
            }
        }
    }

    //тут клей, но вроде и нет необходимости конкретно тут быстро работать
    //комибинирует элементы группы со всеми элементами других групп, в рамках одной группы элементы не комбинируются
    //в возвращенных комбинациях сохраняется порядок переданных групп
    //число комбинаций определяется перемножением числа элементов во всех группах
    //WARN: при использовании yield варианта нужно итерироваться строго последовательно, но это избавляет от необходимости аллоцировать массив на каждую комбинацию
    private static IEnumerable<IEnumerable<(TGroup group, TValue value)>> CombineGroups<TGroup, TValue>(IEnumerable<IGrouping<TGroup, TValue>> groups)
    {
        if (!groups.Any())
        {
            yield break;
        }

        var iterators = groups
            .Select(g => g.Select(v => (g.Key, v)).ToList().AsEnumerable())
            .Select(x => (enumerator: x.GetEnumerator(), source: x))
            .Where(i => i.enumerator.MoveNext())
            .ToArray();

        yield return iterators.Select(i => i.enumerator.Current);

        while (SetNextIteration(0))
        {
            yield return iterators.Select(i => i.enumerator.Current);
        }

        bool SetNextIteration(int i)
        {
            if (i >= iterators.Length)
            {
                return false;
            }

            var iterator = iterators[i];

            if (!iterator.enumerator.MoveNext())
            {
                iterators[i].enumerator = iterators[i].source.GetEnumerator(); //reset enumerator
                iterators[i].enumerator.MoveNext(); //set to first item

                return SetNextIteration(i + 1);
            }

            return true;
        }
    }

    private static IEnumerable<T> SparseIterations<T>(IEnumerable<T> source, int offset, int sparseFactor)
    {
        var current = 0;
        var currentFromOffset = 0;

        foreach (var item in source)
        {
            if (current < offset)
            {
                current++;
                continue;
            }

            if (currentFromOffset % sparseFactor == 0)
            {
                yield return item;
            }

            currentFromOffset++;
        }
    }

    //тут пока хреноватая реализация - в плане пересчета deepRange
    //но можно подумать как сделать по нормальному и вынести в отдельную сущность
    private IEnumerable<DateTimeRange> GetWindowRanges(DateTimeRange range, PeriodInfo deep, PeriodInfo step)
    {
        var deepPeriods = periodSeparator.GetPeriods(range, deep);

        if (!deepPeriods.Any())
        {
            yield break;
        }

        var deepRange = deepPeriods.First();

        yield return deepRange;

        var stepRange = new DateTimeRange(deepRange.EndDateTime, range.EndDateTime);

        if (stepRange.Offset == TimeSpan.Zero)
        {
            yield break;
        }

        var stepPeriods = periodSeparator.GetPeriods(stepRange, step);

        foreach (var stepPeriod in stepPeriods)
        {
            var newStart = deepRange.DateTime + stepPeriod.Offset;

            deepRange = new DateTimeRange(newStart, stepPeriod.EndDateTime);

            yield return deepRange;
        }
    }

    private void ShiftResults(DateTimeRange window)
    {
        Parallel.ForEach(results.ResultsEx,
            //new ParallelOptions() { MaxDegreeOfParallelism = 1 },  //for debug
            (optResult) =>
            {
                ShiftResults(optResult, window);
            }
        );
    }

    private void ShiftResults(OptimizationResultEx optResult, DateTimeRange window)
    {
        var row = new ListViewItem(); //первая ячейка по умолчанию пустая
        var performance = new SystemPerformance(optResult.Performance.Strategy);

        CopyPerformance(performance, optResult.Performance);

        ShiftResults(performance.Results, window.DateTime, window.EndDateTime);

        scorecard.PopulateScorecard(row, performance);

        for (int i = 1; i < row.SubItems.Count; i++)
        {
            string cell = row.SubItems[i].Text;
            optResult.Results[i - 1] = double.TryParse(cell, out var value) ? value : 0.0;
        }

        optResult.PerformanceShifted = performance;
    }

    private static void ShiftResults(SystemResults result, DateTime from, DateTime to)
    {
        OptResultsGraph2DEx.ShiftResults(result, from, to);
    }

    private static void CopyPerformance(SystemPerformance to, SystemPerformance from)
    {
        OptimizationResultEx.CopyPerformance(to, from);
    }

    private void ViewResults()
    {
        txtResult.Clear();

        var headerView = $"current leader {Environment.NewLine} (parameters, range, return %) {Environment.NewLine}";

        txtResult.AppendText(headerView + Environment.NewLine);

        if (windowResults.Count == 0)
        {
            txtResult.AppendText("there's no results");
        }

        foreach (var result in windowResults)
        {
            var rowView = GetResultView(result);

            txtResult.AppendText(rowView + Environment.NewLine);
        }

        ViewGroupResults();
    }

    private static string GetResultView((OptimizationResultEx result, DateTimeRange range) result)
    {
        const string separator = "\t|\t";
        var culture = CultureInfo.InvariantCulture;

        var parameters = string.Join(",", result.result.ParameterValues.Select(x => x.ToString(culture)));

        var returnPercent = GetReturnPercent(result.result.PerformanceShifted.Results);

        var rowView = $"{parameters}{separator}{result.range.ToString("dd.MM.yyyy")}{separator}{returnPercent.ToString("F2", culture)}";

        return rowView;
    }

    private void ViewGroupResults()
    {
        string separator = "====================";
        separator = $"{Environment.NewLine}{separator}{Environment.NewLine}";

        txtGroupResult.Clear();

        var combinationsCount = GetResultGroupsCombinationsCount();

        var combinationsView = $"overall combinations count: {combinationsCount:#,##0}{separator}{Environment.NewLine}";

        txtGroupResult.AppendText(combinationsView);

        if (windowResultGroups.Count == 0)
        {
            txtGroupResult.AppendText("there's no results");
        }

        foreach (var (result, ix) in windowResultGroups.WithIndex(1))
        {
            var resultView = GetGroupResultView(result);

            resultView = $"№ {ix}{separator}{resultView}{Environment.NewLine}";

            txtGroupResult.AppendText(resultView);
        }
    }

    private static string GetGroupResultView((List<OptimizationResultEx> result, DateTimeRange range) result)
    {
        const string separator = "\t|\t";
        var culture = CultureInfo.InvariantCulture;

        var viewBuilder = new StringBuilder();

        var groupHeaderView = $"range: {result.range.ToString("dd.MM.yyyy")}; sys count: {result.result.Count}";
        viewBuilder.AppendLine(groupHeaderView);
        viewBuilder.AppendLine();

        foreach (var (res, returnPercent) in result.result
            .Select(r => (result: r, returnPercent: GetReturnPercent(r.PerformanceShifted.Results)))
            .OrderBy(r => r.returnPercent))
        {
            var parameters = string.Join(",", res.ParameterValues.Select(x => x.ToString(culture)));

            var rowView = $"{parameters}{separator}{returnPercent.ToString("F2", culture)}";

            viewBuilder.AppendLine(rowView);
        }

        return viewBuilder.ToString();
    }

    private static double GetReturnPercent(SystemResults results)
    {
        var equity = results.EquityCurve;
        var returnPercent = equity.Count > 1 ? MathHelper.RatioToPercent(equity[equity.Count - 1] / equity[0]) : 0;
        return returnPercent;
    }

    private List<(List<double> metrics, List<(OptimizationResultEx result, DateTimeRange range)> results)> preOptimizationResults { get; set; } = new();

    private void DoPreOptimization()
    {
        this.preOptimizationResults.Clear();

        FilterResultGroups();

        var approximatedTargetRange = GetApproximatedWorkRange();
        var approximatedMonthsCount = approximatedTargetRange.Ticks / DateTimeConsts.TicksIn30Days;

        var optimizationResults = new ConcurrentBag<(List<double> metrics, List<(OptimizationResultEx result, DateTimeRange range)> results)>();

        Parallel.ForEach(
            GetSerialResults(Environment.ProcessorCount),
            (resultGroupSerial) =>
            {
                foreach (var resultGroup in resultGroupSerial)
                {
                    var returns = resultGroup
                        .Select(g => g.value.PerformanceShifted.Results.EquityCurve)
                        .Select(equity => equity[equity.Count - 1] / equity[0]); //пустые заранее отфильтровали

                    var returnRatio = returns.Aggregate((acc, r) => r * acc);

                    //returnRatio = avgMonthReturnRatio ^ monthsCount
                    var avgMonthReturnRatio = Math.Pow(returnRatio, 1d / approximatedMonthsCount);

                    var returnPercent = MathHelper.RatioToPercent(returnRatio);
                    var avgMonthReturnRepcent = MathHelper.RatioToPercent(avgMonthReturnRatio);

                    List<double> metrics = [returnPercent, avgMonthReturnRepcent];

                    optimizationResults.Add((metrics, resultGroup.Select(x => (x.value, x.group)).ToList()));
                }
            }
        );

        this.preOptimizationResults = optimizationResults.ToList();
    }

    //TODO:
    //при большом количестве результатов отваливается с ООМ
    //надо бы выводить их буферизированно прямо во время расчетов, без промежуточного заполнения в памяти
    //либо продьюсить-консьюмить через channel, если не хочется смешивать расчет и вывод
    private void ViewPreOptimizationResults()
    {
        WritePreOptimizationResultsToFile();
        //return;

        txtResult.Clear();

        const string separator = "\t|\t";
        var culture = CultureInfo.InvariantCulture;

        var metricsRowView = string.Join(separator, ["id", "return %", "avg month return %"]);
        txtResult.AppendText(metricsRowView + Environment.NewLine);

        var dataViewBuilder = new StringBuilder();
        foreach (var (optResult, ix) in preOptimizationResults.WithIndex(1))
        {
            foreach (var result in optResult.results)
            {
                var infoView = GetResultView(result);
                dataViewBuilder.AppendLine(infoView);
            }

            var rowView = string.Join(separator, optResult.metrics.Prepend(ix));
            dataViewBuilder.AppendLine(rowView);
        }

        txtResult.AppendText(dataViewBuilder.ToString());
    }

    private void WritePreOptimizationResultsToFile()
    {
        var directory = $"{Path.GetTempPath()}{nameof(OptResultsWindowAdapteeOptimizer)}";
        var path = $"{directory}\\results.csv";

        Directory.CreateDirectory(directory);
        using var writer = new StreamWriter(new FileStream(path, FileMode.Create));

        const string separator = ",";
        var culture = CultureInfo.InvariantCulture;

        var headerView = string.Join(separator, ["id", "return", "avg_mr"]);
        writer.WriteLine(headerView);

        foreach (var (optResult, ix) in preOptimizationResults.WithIndex(1))
        {
            var metrics = optResult.metrics.Prepend(ix).Select(x => x.ToString(culture));
            var rowView = string.Join(separator, metrics);
            writer.WriteLine(rowView);
        }

        Clipboard.SetText(path);
        MessageBox.Show($"Pre optimization results are writen to the file (path is copied to the clipboard):{Environment.NewLine}{path}");
    }

    private void FilterResultGroups()
    {
        //фильтруем системы без изменения дохода
        windowResultGroups = windowResultGroups
            .Select(g => (results: g.results.Where(r => GetReturnPercent(r.PerformanceShifted.Results) != 0).ToList(), g.range))
            .Where(g => g.results.Count > 0)
            .ToList();
    }

    private TimeSpan GetApproximatedWorkRange()
    {
        var input = GetInputData();
        var sourceRange = results.GetDatesRange().Offset;
        var deepRange = PeriodSeparatorHelper.GetApproximatedSpan(input.PeriodDeep);
        var approximatedWorkRange = sourceRange - deepRange;
        return approximatedWorkRange;
    }

    private double GetResultGroupsCombinationsCount()
    {
        return windowResultGroups.Count == 0 ? 0 : windowResultGroups
            .Where(g => g.results.Count > 0)
            .Aggregate(1d, (cmb, g) => g.results.Count * cmb);
    }

    // Declare controls
    private ComboBox comboDeepPeriodType;
    private NumericUpDown numericDeepPeriodSize;
    private ComboBox comboStepPeriodType;
    private NumericUpDown numericStepPeriodSize;

    private ComboBox comboMetrics;
    private ComboBox comboComparer;
    private TextBox txtMinValue;
    private TextBox txtMaxValue;

    private Button btnCalculate;
    private Button btnOptimize;
    private Button btnPreOptimize;

    private TextBox txtMaxGroup;

    private TextBox txtResult;
    private TextBox txtGroupResult;

    private void InitializeComponents()
    {
        InitializeControls();
    }

    private void InitializeControls()
    {
        // Set form size
        this.Width = 800;
        this.Height = 600;

        // Label "deep" for first period controls
        var lblDeep = new Label() { Left = 10, Top = 10, Width = 40, Text = "deep", AutoSize = true };
        new ToolTip().SetToolTip(lblDeep,
            "length of dates window based on source results will be truncated and recalculated");

        // First row: period type 1 and size
        comboDeepPeriodType = new ComboBox() { Left = 60, Top = 10, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var p in Enum.GetValues(typeof(Period))) comboDeepPeriodType.Items.Add(p);
        comboDeepPeriodType.SelectedItem = Period.Year;

        numericDeepPeriodSize = new NumericUpDown() { Left = 190, Top = 10, Width = 60, Minimum = 1, Maximum = 100, Value = 1 };

        // Label "step" for second period controls
        var lblStep = new Label() { Left = 10, Top = 50, Width = 40, Text = "step", AutoSize = true };
        new ToolTip().SetToolTip(lblStep,
            "length of dates steps to move deep range iterative through source dates range");

        // Second row: period type 2 and size
        comboStepPeriodType = new ComboBox() { Left = 60, Top = 50, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var p in Enum.GetValues(typeof(Period))) comboStepPeriodType.Items.Add(p);
        comboStepPeriodType.SelectedItem = Period.Quarter;

        numericStepPeriodSize = new NumericUpDown() { Left = 190, Top = 50, Width = 60, Minimum = 1, Maximum = 100, Value = 1 };

        // Metric selection
        var lblMetric = new Label() { Left = 300, Top = 10, Text = "Metric:", AutoSize = true };
        comboMetrics = new ComboBox() { Left = 350, Top = 10, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };

        // Comparer selection
        var lblComparer = new Label() { Left = 300, Top = 50, Text = "Method:", AutoSize = true };
        comboComparer = new ComboBox() { Left = 350, Top = 50, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        comboComparer.Items.Add(MaxMetricEqualityComparer.Instance);
        comboComparer.Items.Add(MinMetricEqualityComparer.Instance);
        comboComparer.SelectedIndex = 0;
        new ToolTip().SetToolTip(lblComparer,
            "metric comparision method");

        // Min and Max value
        var lblMin = new Label() { Left = 300, Top = 90, Text = "Out:", AutoSize = true };
        txtMinValue = new TextBox() { Left = 350, Top = 90, Width = 60, Text = "0" };
        new ToolTip().SetToolTip(lblMin,
            $"OUT condition for leader system {Environment.NewLine}" +
            $"leader will be changed if metric become worse than specified value");

        var lblMax = new Label() { Left = 430, Top = 90, Text = "In:", AutoSize = true };
        txtMaxValue = new TextBox() { Left = 480, Top = 90, Width = 60, Text = "1" };
        new ToolTip().SetToolTip(lblMax,
            $"IN condition for systems {Environment.NewLine}" +
            $"system will be accounted if its metric become best than specified value");

        // Calculate button
        btnCalculate = new Button() { Left = 580, Top = 10, Width = 100, Text = "Calculate" };
        btnCalculate.Click += BtnCalculate_Click;
        new ToolTip().SetToolTip(btnCalculate,
            "calculate leader system based on specified metrics OUT and IN condition");

        btnOptimize = new Button() { Left = 580, Top = 50, Width = 100, Text = "Optimize" };
        btnOptimize.Click += BtnOptimize_Click;
        new ToolTip().SetToolTip(btnOptimize,
            $"calculate all combinations of satisfied systems (based on IN condition) {Environment.NewLine}" +
            $"may take very long time, increase step range or intensify IN condition to decrease execution time");

        btnPreOptimize = new Button() { Left = 580, Top = 90, Width = 100, Text = "PreOptimize" };
        btnPreOptimize.Click += BtnPreOptimize_Click;
        new ToolTip().SetToolTip(btnPreOptimize,
            $"calculate all combinations of satisfied systems (based on IN condition) {Environment.NewLine}" +
            $"based only on system ranges returns, there's no source equities combining or scorecard using {Environment.NewLine}" +
            $"so it work faster but gives restricted and inaccurate metrics set");

        var lblMaxGroup = new Label() { Left = 730, Top = 10, Text = "Max group:", AutoSize = true };
        new ToolTip().SetToolTip(lblMaxGroup,
            $"maximum systems count in range group {Environment.NewLine}" +
            $"keep clear to do not limitation");
        txtMaxGroup = new TextBox() { Left = 800, Top = 10, Width = 60, Text = "5" };

        // Result textbox
        txtResult = new TextBox()
        {
            Left = 10,
            Top = 140,
            Width = 500,
            Height = 440,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            ReadOnly = true,
        };

        txtGroupResult = new TextBox()
        {
            Left = 600,
            Top = 140,
            Width = 400,
            Height = 440,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            ReadOnly = true,
        };

        // Add controls to the form
        this.Controls.Add(lblDeep);
        this.Controls.Add(comboDeepPeriodType);
        this.Controls.Add(numericDeepPeriodSize);

        this.Controls.Add(lblStep);
        this.Controls.Add(comboStepPeriodType);
        this.Controls.Add(numericStepPeriodSize);

        this.Controls.Add(lblMetric);
        this.Controls.Add(comboMetrics);
        this.Controls.Add(lblComparer);
        this.Controls.Add(comboComparer);
        this.Controls.Add(lblMin);
        this.Controls.Add(txtMinValue);
        this.Controls.Add(lblMax);
        this.Controls.Add(txtMaxValue);
        this.Controls.Add(lblMaxGroup);
        this.Controls.Add(txtMaxGroup);

        this.Controls.Add(btnCalculate);
        this.Controls.Add(btnOptimize);
        this.Controls.Add(btnPreOptimize);
        this.Controls.Add(txtResult);
        this.Controls.Add(txtGroupResult);
    }

    private struct PeriodMetricsInput
    {
        public PeriodInfo PeriodDeep;
        public PeriodInfo PeriodStep;
        public string MetricName;
        public IComparer<double> MetricComparer;
        public double MinValue;
        public double MaxValue;
        public int? MaxGroup;

        public bool BetterThanMinValue(double metric) => MetricComparer.Compare(metric, MinValue) >= 0;
        public bool BetterThanMaxValue(double metric) => MetricComparer.Compare(metric, MaxValue) >= 0;
    }

    private PeriodMetricsInput GetInputData()
    {
        return new PeriodMetricsInput
        {
            PeriodDeep = new((Period)comboDeepPeriodType.SelectedItem, (int)numericDeepPeriodSize.Value),
            PeriodStep = new((Period)comboStepPeriodType.SelectedItem, (int)numericStepPeriodSize.Value),
            MetricName = comboMetrics.SelectedItem.ToString(),
            MetricComparer = (IComparer<double>)comboComparer.SelectedItem,
            MinValue = double.TryParse(txtMinValue.Text, out double minVal) ? minVal : 0,
            MaxValue = double.TryParse(txtMaxValue.Text, out double maxVal) ? maxVal : 0,
            MaxGroup = int.TryParse(txtMaxGroup.Text, out int maxGroupVal) ? maxGroupVal : null
        };
    }

    private class MaxMetricEqualityComparer : IComparer<double>
    {
        public static MaxMetricEqualityComparer Instance { get; } = new();
        public int Compare(double x, double y) => x.CompareTo(y);
        public override string ToString() => "Maximize";
    }

    private class MinMetricEqualityComparer : IComparer<double>
    {
        public static MinMetricEqualityComparer Instance { get; } = new();
        public int Compare(double x, double y) => -x.CompareTo(y);
        public override string ToString() => "Minimize";
    }

    private void BtnCalculate_Click(object sender, EventArgs e)
    {
        //validation
        if (!ValidateInput(out var error))
        {
            MessageBox.Show(error);
            return;
        }

        //calc
        DoCalc();
        ViewResults();
    }


    private void BtnOptimize_Click(object sender, EventArgs e)
    {
        //validation
        if (!ValidateInput(out var error))
        {
            MessageBox.Show(error);
            return;
        }

        //calc
        DoCalc();
        DoOptimization();
        ViewOptimizationResults();
    }

    private void BtnPreOptimize_Click(object sender, EventArgs e)
    {
        //validation
        if (!ValidateInput(out var error))
        {
            MessageBox.Show(error);
            return;
        }

        //calc
        DoCalc();
        DoPreOptimization();
        ViewPreOptimizationResults();
    }

    private bool ValidateInput(out string error)
    {
        var builder = new StringBuilder();

        if (!double.TryParse(txtMinValue.Text, out _))
        {
            builder.AppendLine("Invalid min value");
        }
        if (!double.TryParse(txtMaxValue.Text, out _))
        {
            builder.AppendLine("Invalid max value");
        }
        if ((int)numericDeepPeriodSize.Value < 1)
        {
            builder.AppendLine("Deep period must have positive range");
        }
        if ((int)numericStepPeriodSize.Value < 1)
        {
            builder.AppendLine("Step period must have positive range");
        }
        if (string.IsNullOrWhiteSpace(txtMaxGroup.Text) is false &&
            int.TryParse(txtMaxGroup.Text, out int maxGroupValue) is false &&
            maxGroupValue < 0)
        {
            builder.AppendLine("Invalid max group value");
        }

        error = builder.ToString();

        return string.IsNullOrWhiteSpace(error);
    }
}
