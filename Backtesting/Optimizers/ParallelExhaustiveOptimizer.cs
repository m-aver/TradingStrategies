using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using WealthLab;
using WealthLab.Visualizers;
using Fidelity.Components;
using System.Linq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime;

//TODO:
//побаловаться настройками GC
//разобраться куда утекает память | UPD: предположение что просто винда выделяет память под процесс заранее, по метрикам студии все норм
//можно зафигачить базовый класс оптимизеру, который будет генерировать окно в интерфейсе под настройки (см. монтекарло)
//признак запущенной оптимизации в WS Wrapper для перфоманса
//записать изменения в исходном коде
//причесать, закомитить
//добавить выбор скорекардов
//свой скорекард с основными параметрами, MS123 слишком много жрет цп
//удалить один лишний экзекутор
//убедиться результаты одинаковы

namespace TradingStrategies.Backtesting.Optimizers
{
    /// <summary>
    /// Represents scope of data for one separate optimization run
    /// </summary>
    internal class ExecutionScope
    {
        public TradingSystemExecutor Executor  { get; set; }
        public WealthScript Script { get; set; }
        public StrategyScorecard Scorecard { get; set; }
        public SystemPerformance Result { get; set; }
        public Strategy Strategy { get; set; }
        public List<Bars> BarsSet { get; set; }
        public List<ListViewItem> ResultRows { get; set; }

        public ExecutionScope(
            TradingSystemExecutor executor,
            WealthScript script,
            StrategyScorecard scorecard,
            SystemPerformance result,
            Strategy strategy,
            List<Bars> barsSet,
            List<ListViewItem> resultRows)
        {
            this.Executor = executor;
            this.Script = script;
            this.Scorecard = scorecard;
            this.Result = result;
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
        private ListView optimizationResultListView;
        private IStrategyParametersIterator parametersIterator;

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
            countDown.Wait();
            mainWatch.Reset();
            base.RunCompleted(results);
            PopulateUI();
            countDown.Dispose();

            FullCollect();
        }

        /// <summary>
        /// Initializes the optimizer. Called when optimization method has been selected. Run once for multiple optimization sessions.
        /// </summary>
        public override void Initialize()
        {
            numThreads = Environment.ProcessorCount;
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
                foreach (var (symbol, i) in parentExecutor.DataSet.Symbols.Select((x, i) => (x, i)))
                {
                    var bars = parentExecutor.BarsLoader.GetData(parentExecutor.DataSet, symbol);
                    dataSetBars.Add(symbol, bars.WithHash(i + 1));
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
            
            StrategyManager strategyManager = new StrategyManager();
            SettingsManager settingsManager = new SettingsManager();
            settingsManager.RootPath = Application.UserAppDataPath + System.IO.Path.DirectorySeparatorChar + "Data";
            settingsManager.FileName = "WealthLabConfig.txt";
            settingsManager.LoadSettings();

            // check if this strategy can be run by this optimizer
            this.supported = true;
            this.barsCache = new Dictionary<string, Bars>(numThreads);

            var testExecutor = CopyExecutor(parentExecutor);
            var testScript = strategyManager.GetWealthScriptObject(this.Strategy);
            var testStrategy = CopyStrategy(this.Strategy);

            testExecutor.ExternalSymbolRequested += this.OnLoadSymbol;
            testExecutor.ExternalSymbolFromDataSetRequested += this.OnLoadSymbolFromDataSet;

            try
            {
                testExecutor.Execute(testStrategy, testScript, null, dataSetBars.Values.ToList());
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
            Parallel.For(0, numThreads, 
                i =>
                {
                    this.executors[i] = new ExecutionScope(
                        CopyExecutor(parentExecutor),
                        strategyManager.GetWealthScriptObject(this.Strategy),
                        GetSelectedScoreCard(settingsManager),
                        null,
                        CopyStrategy(this.Strategy),
                        dataSetBars.Values.ToList(),
                        new List<ListViewItem>(runs)
                    );
                    this.executors[i].Executor.ExternalSymbolRequested += this.OnLoadSymbol;
                    this.executors[i].Executor.ExternalSymbolFromDataSetRequested += this.OnLoadSymbolFromDataSet;
                    SynchronizeWealthScriptParameters(this.executors[i].Script, this.WealthScript);
                });

            optimizationResultListView = (ListView)((TabControl)((UserControl)this.Host).Controls[0]).TabPages[1].Controls[0];

            //initialize perfomance metrics
            const int metricsCount = 10;
            metrics = writeMetrics
                ? (IOptimizerPerfomanceMetrics) new OptimizerPerfomanceMetrics(numThreads + 1, runs, metricsCount)
                : (IOptimizerPerfomanceMetrics) new MockOptimizerPerfomanceMetrics();

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

            var x = NextRunInternal();

            mainWatch.Restart();

            return x;
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
                        countDown.Signal();
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

                                AddResultsToUI(threadNum);
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
        private void AddResultsToUI(int index)
        {
            var uiWatch = Stopwatch.StartNew();

            var executors = this.executors[index];

            ListViewItem row = new ListViewItem();
            row.SubItems[0].Text = executors.Executor.DataSet.Name;
            foreach (var parameter in executors.Script.Parameters)
                row.SubItems.Add(parameter.Value.ToString());

            executors.Scorecard.PopulateScorecard(row, executors.Result);
            executors.ResultRows.Add(row);

            metrics.SetTime("ui", uiWatch.ElapsedMilliseconds, index, currentRun);
        }

        private void PopulateUI()
        {
            var rows = executors.SelectMany(x => x.ResultRows).ToArray();

            if (optimizationResultListView.InvokeRequired)
            {
                //выполняется на главном потоке
                //может привести к дедлоку если главный поток лочится на счетчике
                optimizationResultListView.Invoke(
                    new Action<ListView, ListViewItem[]>((view, newRows) => view.Items.AddRange(newRows)),
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

        private static void SynchronizeWealthScriptParameters(WealthScript wsTarget, WealthScript wsSource)
        {
            wsTarget.Parameters.Clear();

            foreach (var parameter in wsSource.Parameters)
            {
                wsTarget.Parameters.Add(
                    CopyParameter(parameter));
            }
        }

        private static StrategyParameter CopyParameter(StrategyParameter old)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TradingSystemExecutor CopyExecutor(TradingSystemExecutor source)
        {
            var target = new TradingSystemExecutor();

            target.ApplySettings(source);

            target.FundamentalsLoader = source.FundamentalsLoader;
            target.BarsLoader = source.BarsLoader;
            target.DataSet = source.DataSet;
            target.Commission = source.Commission;
            target.PosSize = CopyPositionSize(source.PosSize);
            target.BenchmarkBuyAndHoldON = false;

            return target;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PositionSize CopyPositionSize(PositionSize source)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Strategy CopyStrategy(Strategy source)
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

        private static TradingSystemExecutor ExtractExecutor(WealthScript script)
        {
            var tsField = typeof(WealthScript)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .First(x => x.FieldType == typeof(TradingSystemExecutor));
            var tsExecutor = tsField.GetValue(script) as TradingSystemExecutor;
            return tsExecutor;
        }

        private static StrategyScorecard GetSelectedScoreCard(SettingsManager sm)
        {
            if (sm.Settings.ContainsKey("Optimization.Scorecard"))
            {
                if (string.CompareOrdinal(sm.Settings["Optimization.Scorecard"], "Basic Scorecard") == 0)
                    return new BasicScorecard();
            }
            return new ExtendedScorecard();
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

        private static void FullCollect()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }

        ~ParallelExhaustiveOptimizer()
        {
            countDown?.Dispose();
        }
    }
}