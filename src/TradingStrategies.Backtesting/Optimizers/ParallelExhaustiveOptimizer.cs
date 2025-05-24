using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using WealthLab;
using Fidelity.Components;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime;
using TradingStrategies.Backtesting.Optimizers.Scorecards;
using TradingStrategies.Backtesting.Optimizers.Utility;

//есть вариант увеличить перфоманс в 3-4 раза 
//надо фильтровать результаты стратегии в самой стратегии (по примеру BasicExFilteringScorecard), по окончанию обработки датасета
//(нужно избежать вызова TradingSystemExecutor.ApplyPositionSize, он довольно дорогой даже если позиций нет)
//пока нашел два варианта:
//выставлять TradingSystemExecutor.BuildEquityCurves в false, но для следующего запуска его нужно вернуть в true
//выбрасывать эксепшен, но надо заранее выставить TradingSystemExecutor.ExceptionEvents в false (можно из экзекутора, а не стратегии)
//у обоих вариантов есть свои минусы и плюсы, но в любом случае придется переделывать прицип распараллеливания
//поскольку в текущем варианте, если хотя бы для одного из параллельных запусков не выполнилась фильтрация, он будет задерживать все остальные

//студия может сожрать много рама
//тогда GC работает активнее и перфоманс понижается
//настройки GC: <gcServer enabled="true"/> <gcConcurrent enabled="true"/>
//также если открыт Omen Gaming Hub, то сильно режется максимальная утилизация ЦП (85% -> 55%)
//видимо по дефолту активируется какой-то экономный профиль

namespace TradingStrategies.Backtesting.Optimizers
{
    /// <summary>
    /// Represents scope of data for one separate optimization run
    /// </summary>
    internal class ExecutionScope
    {
        public TradingSystemExecutor Executor { get; }
        public WealthScript Script { get; }
        public StrategyScorecard Scorecard { get; }
        public SystemPerformance Result { get; set; }
        public Strategy Strategy { get; }
        public List<Bars> BarsSet { get; }
        public List<ListViewItem> ResultRows { get; }

        public ExecutionScope(
            TradingSystemExecutor executor,
            WealthScript script,
            StrategyScorecard scorecard,
            Strategy strategy,
            List<Bars> barsSet,
            List<ListViewItem> resultRows)
        {
            this.Executor = executor;
            this.Script = script;
            this.Scorecard = scorecard;
            this.Strategy = strategy;
            this.BarsSet = barsSet;
            this.ResultRows = resultRows;
        }
    }

    /// <summary>
    /// Implements Multithreaded Optimization
    /// </summary>
    public class ParallelExhaustiveOptimizer : Optimizer
    {
        private int numThreads;
        private ExecutionScope[] executors;
        private CountdownEvent countDown;
        private List<StrategyParameter> paramValues;
        private bool supported;
        private Dictionary<string, Bars> barsCache;
        private Dictionary<string, Bars> dataSetBars;
        private TradingSystemExecutor parentExecutor;
        private IStrategyParametersIterator parametersIterator;
        private IScorecardProvider scorecardProvider;

        //for metrics
        private const bool writeMetrics = false;
        private int mainThread => numThreads + 1 - 1;
        private int currentRun = 0;
        private Stopwatch mainWatch = new Stopwatch();
        private IOptimizerPerfomanceMetrics metrics;

        public override string Description => "Parallel Optimizer (Exhaustive)";
        public override string FriendlyName => Description;

        public override void RunCompleted(OptimizationResultList results)
        {
            this.countDown.Wait();
            mainWatch.Reset();
            base.RunCompleted(results);
            PopulateUI();
            this.countDown.Dispose();

            FullCollect();
        }

        /// <summary>
        /// Initializes the optimizer. Called when optimization method has been selected. Run once for multiple optimization sessions.
        /// </summary>
        public override void Initialize()
        {
            numThreads = Environment.ProcessorCount;

            var settingsManager = new SettingsManager();
            settingsManager.RootPath = Application.UserAppDataPath + Path.DirectorySeparatorChar + "Data";
            settingsManager.FileName = "WealthLabConfig.txt";
            settingsManager.LoadSettings();

            scorecardProvider = ScorecardProviderFactory.Create(settingsManager, this);
        }

        //RunsRequired = NumberOfRuns * datasets-count
        public override double NumberOfRuns
        {
            get
            {
                var iterator = CreateParametersIterator();
                var runs = iterator.RunsCount();
                runs = Math.Max(runs / numThreads, 1);
                return runs;
            }
        }

        /// <summary>
        /// The very first run for this optimization. Sets up everything for the entire optimization. Run once for one optimization session.
        /// </summary>
        public override void FirstRun()
        {
            mainWatch.Restart();

            FullCollect();

            parentExecutor = ExtractExecutor(this.WealthScript);
            if (parentExecutor == null)
            {
                var message = $"cannot load executor, you must run strategy firstly on that dataset everywhere";
                Debug.Print(message);
                MessageBox.Show(message);
                this.supported = false;
                return;
            }
            parentExecutor.BenchmarkBuyAndHoldON = false;

            //extract all data set bars
            dataSetBars = new Dictionary<string, Bars>();
            try
            {
                foreach (var symbol in parentExecutor.DataSet.Symbols)
                {
                    var bars = parentExecutor.BarsLoader.GetData(parentExecutor.DataSet, symbol);
                    dataSetBars.Add(symbol, bars);
                }
            }
            catch (Exception e)
            {
                var message = $"Cannot load bars.{Environment.NewLine}Error: {e.ToString()}";
                Debug.Print(message);
                MessageBox.Show(message);
                this.supported = false;
                return;
            }

            // check if this strategy can be run by this optimizer
            this.supported = true;
            this.barsCache = new Dictionary<string, Bars>(numThreads);

            var strategyManager = new StrategyManager();
            var testScript = strategyManager.GetWealthScriptObject(this.Strategy);
            var testExecutor = CopyExecutor(parentExecutor);
            var testStrategy = CopyStrategy(this.Strategy);
            var testBars = dataSetBars.Values.Select((x, i) => x.Prepare(i + 1)).ToList();

            testExecutor.ExternalSymbolRequested += this.OnLoadSymbol;
            testExecutor.ExternalSymbolFromDataSetRequested += this.OnLoadSymbolFromDataSet;

            try
            {
                testExecutor.Execute(testStrategy, testScript, null, testBars);
            }
            catch (Exception e)
            {
                var message =
                    $"This strategy cannot be run in multi-threaded mode." +
                    $"{Environment.NewLine}Error: {e.ToString()}";
                Debug.Print(message);
                MessageBox.Show(message);
                this.supported = false;
                return;
            }

            //initialize parameters
            this.parametersIterator = CreateParametersIterator();
            this.paramValues = parametersIterator.CurrentParameters.ToList();

            //initialize parallel executors
            this.executors = new ExecutionScope[numThreads];

            var runs = (int)NumberOfRuns;
            Parallel.For(0, numThreads, i =>
            {
                var offset = i * dataSetBars.Values.Count;
                this.executors[i] = new ExecutionScope(
                    CopyExecutor(parentExecutor),
                    strategyManager.GetWealthScriptObject(this.Strategy),
                    CopySelectedScoreCard(),
                    CopyStrategy(this.Strategy),
                    dataSetBars.Values.Select((x, j) => x.Prepare(j + 1 + offset)).ToList(),
                    new List<ListViewItem>(runs)
                );
                //this.executors[i].Executor.ExternalSymbolRequested += this.OnLoadSymbol;
                //this.executors[i].Executor.ExternalSymbolFromDataSetRequested += this.OnLoadSymbolFromDataSet;
                SynchronizeWealthScriptParameters(this.executors[i].Script, this.WealthScript);
            });

            //initialize perfomance metrics
            const int metricsCount = 10;
            metrics = writeMetrics
                ? new OptimizerPerfomanceMetrics(numThreads + 1, runs, metricsCount)
                : new MockOptimizerPerfomanceMetrics();

            metrics.SetTime("firstRun", mainWatch.ElapsedMilliseconds, mainThread, currentRun);
            mainWatch.Restart();

            this.countDown = new CountdownEvent(numThreads);
            this.countDown.Signal(countDown.InitialCount);  //release before first run
        }

        public override bool NextRun(SystemPerformance sp, OptimizationResult or)
        {
            metrics.SetTime("all_end", mainWatch.ElapsedMilliseconds, mainThread, currentRun);

            currentRun++;
            mainWatch.Restart();

            var next = NextRunInternal();

            mainWatch.Restart();

            return next;
        }

        /// <summary>
        /// Implements parallel runs - each logical "next run" results in multiple parallel runs
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool NextRunInternal()
        {
            this.countDown.Wait();

            metrics.SetTime("all_countDown", mainWatch.ElapsedMilliseconds, mainThread, currentRun);

            if (!this.supported)
                return false;

            mainWatch.Restart();

            this.countDown.Reset();

            for (int i = 0; i < numThreads; i++)
            {
                var executors = this.executors[i];

                if (SetNextRunParameters())
                {
                    if (i == 0)
                    {
                        // set params to execute by main thread after NextRun
                        for (int j = 0; j < this.paramValues.Count; j++)
                            this.WealthScript.Parameters[j].Value = this.paramValues[j].Value;
                        this.countDown.Signal();
                        continue;
                    }

                    for (int j = 0; j < this.paramValues.Count; j++)
                        executors.Script.Parameters[j].Value = this.paramValues[j].Value;

                    ThreadPool.QueueUserWorkItem((e) =>
                    {
                        var watch = Stopwatch.StartNew();

                        var threadNum = (int)e;
                        try
                        {
                            //lock (this)   //for debug
                            {
                                ExecuteOne(threadNum);

                                PrepareResultsToUI(threadNum);
                            }
                        }
                        catch
                        {
                            executors.Result = null;
                        }
                        finally
                        {
                            executors.Executor.Clear();
                            this.countDown.Signal();

                            watch.Stop();
                            metrics.SetTime("all", watch.ElapsedMilliseconds, threadNum, currentRun);
                        }
                    }, i);
                }
                else
                {
                    // this is the last run
                    this.supported = false;
                    for (int j = i; j < numThreads; j++)
                    {
                        this.executors[j].Result = null;
                        this.countDown.Signal();
                    }
                    if (i == 0)
                        return false; // catch the boundary condition
                    break;
                }
            }

            metrics.SetTime("all_runThreads", mainWatch.ElapsedMilliseconds, mainThread, currentRun);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExecuteOne(int sys)
        {
            var watch = Stopwatch.StartNew();

            var executors = this.executors[sys];

            var allBars = executors.BarsSet;
            var ts = executors.Executor;
            var strategy = executors.Strategy;

            metrics.SetTime("executor", watch.ElapsedMilliseconds, sys, currentRun);
            watch.Restart();

            ts.Initialize();
            ts.Execute(strategy, executors.Script, null, allBars);
            executors.Result = ts.Performance;

            metrics.SetTime("script", watch.ElapsedMilliseconds, sys, currentRun);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrepareResultsToUI(int index)
        {
            var uiWatch = Stopwatch.StartNew();

            var executors = this.executors[index];

            ListViewItem row = new ListViewItem();
            row.SubItems[0].Text = executors.Executor.DataSet.Name;
            foreach (var parameter in executors.Script.Parameters)
                row.SubItems.Add(parameter.Value.ToString());

            executors.Scorecard.PopulateScorecard(row, executors.Result);
            executors.ResultRows.Add(row);

            //for executing strategy by double click on result row
            var optimizationResult = new OptimizationResult()
            {
                Symbol = executors.Executor.DataSet.Name,
                ParameterValues = executors.Script.Parameters.Select(x => x.Value).ToList(),
            };
            row.Tag = optimizationResult;

            metrics.SetTime("ui", uiWatch.ElapsedMilliseconds, index, currentRun);
        }

        private void PopulateUI()
        {
            var optimizationResultListView = (ListView)((TabControl)((UserControl)this.Host).Controls[0]).TabPages[1].Controls[0];

            var rows = executors.SelectMany(x => x.ResultRows).ToArray();

            if (optimizationResultListView.InvokeRequired)
            {
                //выполняется на главном потоке
                //может привести к дедлоку если главный поток лочится на счетчике
                optimizationResultListView.Invoke(
                    static (ListView view, ListViewItem[] newRows) => view.Items.AddRange(newRows),
                    optimizationResultListView, rows);
            }
            else
            {
                optimizationResultListView.Items.AddRange(rows);
            }

            foreach (var executor in executors)
            {
                executor.ResultRows.Clear();
            }
        }

        internal static void SynchronizeWealthScriptParameters(WealthScript wsTarget, WealthScript wsSource)
        {
            wsTarget.Parameters.Clear();

            foreach (var parameter in wsSource.Parameters)
            {
                wsTarget.Parameters.Add(
                    CopyParameter(parameter));
            }
        }

        internal static StrategyParameter CopyParameter(StrategyParameter old)
        {
            return new StrategyParameter(
                name: old.Name,
                value: old.Value,
                start: old.Start,
                stop: old.Stop,
                step: old.Step,
                description: old.Description)
            {
                IsEnabled = old.IsEnabled,
                DefaultValue = old.DefaultValue
            };
        }

        /// <summary>
        /// Increments strategy parameter values for the next optimization run based on exhaustive optimization
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SetNextRunParameters()
        {
            return this.parametersIterator.MoveNext();
        }

        private IStrategyParametersIterator CreateParametersIterator(bool fromStart = true)
        {
            var parameters = this.WealthScript.Parameters.Select(CopyParameter).ToArray();
            var iterator = new StrategyParametersIterator(parameters);
            if (fromStart)
                iterator.Reset();
            return iterator;
        }

        internal static TradingSystemExecutor CopyExecutor(TradingSystemExecutor source)
        {
            var target = new TradingSystemExecutor();

            target.ApplySettings(source);

            target.FundamentalsLoader = source.FundamentalsLoader;
            target.BarsLoader = source.BarsLoader;
            target.DataSet = source.DataSet;
            target.Commission = source.Commission;
            target.PosSize = CopyPositionSize(source.PosSize);
            target.BenchmarkBuyAndHoldON = false;
            target.StrategyWindowID = source.StrategyWindowID;
            target.ExceptionEvents = false;

            return target;
        }

        internal static PositionSize CopyPositionSize(PositionSize source)
        {
            var target = new PositionSize()
            {
                Mode = source.Mode,
                RawProfitDollarSize = source.RawProfitDollarSize,
                RawProfitShareSize = source.RawProfitShareSize,
                StartingCapital = source.StartingCapital,
                DollarSize = source.DollarSize,
                ShareSize = source.ShareSize,
                PctSize = source.PctSize,
                RiskSize = source.RiskSize,
                SimuScriptName = source.SimuScriptName,
                PosSizerConfig = source.PosSizerConfig,
                MarginFactor = source.MarginFactor,
                OverrideShareSize = source.OverrideShareSize,
            };

            //это видимо оффициальный способ
            //var target = PositionSize.Parse(source.ToString());

            return target;
        }

        internal static Strategy CopyStrategy(Strategy source)
        {
            var target = new Strategy()
            {
                ID = Guid.NewGuid(),

                Name = source.Name,
                Code = source.Code,
                Description = source.Description,
                Author = source.Author,
                CreationDate = source.CreationDate,
                LastModified = source.LastModified,
                StrategyType = source.StrategyType,
                AccountNumber = source.AccountNumber,
                NetworkDrivePath = source.NetworkDrivePath,
                FileName = source.FileName,
                Folder = source.Folder,
                WealthScriptType = source.WealthScriptType,
                URL = source.URL,
                ParameterValues = source.ParameterValues,
                DataSetName = source.DataSetName,
                Symbol = source.Symbol,
                DataScale = source.DataScale,
                PositionSize = source.PositionSize,
                DataRange = source.DataRange,
                Indicators = source.Indicators,
                Rules = source.Rules,
                SinglePosition = source.SinglePosition,
                References = source.References,
                StartingEquity = source.StartingEquity,
                MarginFactor = source.MarginFactor,
                Origin = source.Origin,
                CombinedStrategyChildren = source.CombinedStrategyChildren,
                PanelSize = source.PanelSize,
                PreferredValues = source.PreferredValues,
                Tag = source.Tag,
                UsePreferredValues = source.UsePreferredValues,
            };

            return target;
        }

        internal static TradingSystemExecutor ExtractExecutor(WealthScript script)
        {
            var tsField = typeof(WealthScript)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .First(x => x.FieldType == typeof(TradingSystemExecutor));
            var tsExecutor = tsField.GetValue(script) as TradingSystemExecutor;
            return tsExecutor;
        }

        private StrategyScorecard CopySelectedScoreCard()
        {
            var scorecard = scorecardProvider.GetSelectedScorecard();
            scorecard = (StrategyScorecard)Activator.CreateInstance(scorecard.GetType());
            return scorecard;
        }

        /// <summary>
        /// Callback for the executor to get external symbols
        /// </summary>
        private void OnLoadSymbol(Object sender, LoadSymbolEventArgs args)
        {
            if (barsCache.ContainsKey(args.Symbol))
            {
                args.SymbolData = barsCache[args.Symbol];
            }
            else
            {
                lock (this.WealthScript)
                {
                    // check no other thread got here first
                    if (!barsCache.ContainsKey(args.Symbol))
                    {
                        this.WealthScript.SetContext(args.Symbol, false);
                        barsCache[args.Symbol] = this.WealthScript.Bars;
                        this.WealthScript.RestoreContext();
                    }
                    args.SymbolData = barsCache[args.Symbol];
                }
            }
        }

        /// <summary>
        /// Callback for the executor to get external symbols
        /// </summary>
        private void OnLoadSymbolFromDataSet(Object sender, LoadSymbolFromDataSetEventArgs args)
        {
            if (barsCache.ContainsKey(args.Symbol))
            {
                args.Bars = barsCache[args.Symbol];
            }
            else
            {
                lock (this.WealthScript)
                {
                    // check no other thread got here first
                    if (!barsCache.ContainsKey(args.Symbol))
                    {
                        this.WealthScript.SetContext(args.Symbol, false);
                        barsCache[args.Symbol] = this.WealthScript.Bars;
                        this.WealthScript.RestoreContext();
                    }
                    args.Bars = barsCache[args.Symbol];
                }
            }
        }

        internal static void FullCollect()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }

        ~ParallelExhaustiveOptimizer()
        {
            this.countDown?.Dispose();
        }
    }
}