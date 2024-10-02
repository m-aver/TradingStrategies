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

namespace TradingStrategies.Backtesting.Optimizers
{
    static class MainModuleInstance
    {
        private static object _instance;
        private static PropertyInfo _dataSources;

        static MainModuleInstance()
        {
            Initialize();
        }

        /// <summary>
        /// Get Wealth-Lab Dev/Pro exe assembly
        /// </summary>
        private static Assembly GetWLAssembly()
        {
            Assembly wlAssembly = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Contains("WealthLabDev") || 
                    assembly.FullName.Contains("WealthLabPro"))
                {
                    wlAssembly = assembly;
                    break;
                }
            }

            if (wlAssembly == null) 
                throw new Exception("Wealth-Lab not found.");

            return wlAssembly;
        }

        /// <summary>
        /// Set Main Module Instance
        /// </summary>
        private static void SetMainModuleInstance()
        {
            Type mainModuleType = null;

            foreach (Type wlAssemblyType in GetWLAssembly().GetTypes())
            {
                Type[] interfaces = wlAssemblyType.GetInterfaces();

                bool wlpDetected = false;
                int cnt = 0;
                foreach (Type wlInterface in interfaces)
                {
                    if (wlInterface == typeof(WealthLab.IAuthenticationHost) ||
                        (wlInterface == typeof(WealthLab.IConnectionStatus)) ||
                        (wlInterface == typeof(WealthLab.IMenuItemAdder)))
                        cnt++;

                    if (cnt >= 3)
                        wlpDetected = true;
                }
                if (wlAssemblyType.FullName == "WealthLabPro.MainModule" || wlpDetected)
                {
                    mainModuleType = wlAssemblyType;
                    break;
                }
            }

            if (mainModuleType == null) 
                throw new Exception("MainModule not found.");

            FieldInfo fiInstance = null;

            foreach (FieldInfo field in mainModuleType.GetFields())
            {
                if (field.FieldType == mainModuleType)
                {
                    fiInstance = field;
                    break;
                }
            }

            if (fiInstance == null) 
                throw new Exception("MainModule.Instance not found.");

            _instance = fiInstance.GetValue(null);
        }

        private static void Initialize()
        {
            SetMainModuleInstance();
            SetDataSourcesInfos();
        }

        /// <summary>
        /// Find DataSources
        /// </summary>
        private static void SetDataSourcesInfos()
        {
            foreach (PropertyInfo property in _instance.GetType().GetProperties())
            {
                if (property.PropertyType == typeof(DataSourceManager))
                {
                    _dataSources = property;
                }
            }

            if ((_dataSources == null)) 
                throw new Exception("DataSourceManager field not found.");
        }

        /// <summary>
        /// DataSourceManager
        /// </summary>
        public static DataSourceManager GetDataSources()
        {
            return (DataSourceManager)_dataSources.GetValue(_instance, new object[] { });
        }
    }

    // for debug
    internal class ParamInfo
    {
        public double MaPeriod { get; set; }
        public double MaPercent { get; set; }
        public double SigmoidOffset { get; set; }
        public double SigmoidStretch { get; set; }
        public double NetProfit { get; set; }
    }

    /// <summary>
    /// Implements Multithreaded Optimization
    /// </summary>
    public class ParallelExhaustiveOptimizer : Optimizer
    {
        private int numThreads;
        private (TradingSystemExecutor tse, WealthScript ws, StrategyScorecard ss)[] executors;
        private SystemPerformance[] results;
        private CountdownEvent countDown;
        private List<StrategyParameter> paramValues;
        private bool supported;
        private Dictionary<string, Bars> barsCache;

        private ConcurrentQueue<ParamInfo> paramInfos = new ConcurrentQueue<ParamInfo>();
        private Dictionary<string, Bars> dataSetBars = new Dictionary<string, Bars>();
        private SettingsManager settingsManager;

        public override string Description => "Parallel Optimizer (Exhaustive)";
        public override string FriendlyName => Description;

        //for debug
        public override void RunCompleted(OptimizationResultList results)
        {
            base.RunCompleted(results);
        }

        /// <summary>
        /// Initializes the optimizer
        /// </summary>
        public override void Initialize()
        {
            numThreads = Environment.ProcessorCount;
        }

        /// <summary>
        /// Returns the number of runs required for the selected strategy
        /// </summary>
        public override double NumberOfRuns
        {
            get
            {
                double runs = 1.0;
                foreach (StrategyParameter parameter in WealthScript.Parameters)
                    runs *= NumberOfRunsPerParameter(parameter);
                runs = Math.Max(Math.Floor(runs / numThreads), 1);
                if (runs > int.MaxValue)
                    runs = int.MaxValue; // otherwise the progress bar is broken
                return runs;
            }
        }

        /// <summary>
        /// Returns the number of runs required for a single strategy parameter
        /// </summary>
        private double NumberOfRunsPerParameter(StrategyParameter parameter)
        {
            if ((parameter.Start < parameter.Stop && parameter.Step > 0) || 
                (parameter.Start > parameter.Stop && parameter.Step < 0))
            {
                return Math.Max(
                    Math.Floor((parameter.Stop - parameter.Start + parameter.Step) / parameter.Step), 
                    1);
            }

            return 1;
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

        /// <summary>
        /// The very first run for this optimization. Sets up everything for the entire optimization
        /// </summary>
        public override void FirstRun()
        {
            // check if this strategy can be run on multiple threads
            this.supported = true;
            this.barsCache = new Dictionary<string, Bars>(numThreads);
            StrategyManager stm = new StrategyManager();
            SettingsManager sm = new SettingsManager();
            settingsManager = sm;
            sm.RootPath = Application.UserAppDataPath + System.IO.Path.DirectorySeparatorChar + "Data";
            sm.FileName = "WealthLabConfig.txt";
            sm.LoadSettings();
            PositionSize ps = GetPositionSize(this.WealthScript);
            TradingSystemExecutor tse = CreateExecutor(this.Strategy, ps, sm, this.WealthScript);
            tse.ExternalSymbolRequested += this.OnLoadSymbol;
            tse.ExternalSymbolFromDataSetRequested += this.OnLoadSymbolFromDataSet;
            WealthScript ws = stm.GetWealthScriptObject(this.Strategy);
            try
            {
                tse.Execute(ws, this.WealthScript.Bars);
            }
            catch (Exception e)
            {
                Debug.Print(e.Message + "\n" + e.StackTrace);
                while (e.InnerException != null)
                {
                    Debug.Print(e.InnerException.Message + "\n" + e.InnerException.StackTrace);
                    e = e.InnerException;
                }
                MessageBox.Show("This strategy cannot be run in multi-threaded mode." +
                    "\nError: " + e.Message + "\n" + e.StackTrace);
                this.supported = false;
                return;
            }
            this.countDown = new CountdownEvent(numThreads);
            this.results = new SystemPerformance[numThreads];
            this.executors = new (TradingSystemExecutor, WealthScript, StrategyScorecard)[numThreads];
            Parallel.For(0, numThreads, i =>
            {
                this.executors[i] = (
                    CreateExecutor(this.Strategy, ps, sm, this.WealthScript),
                    stm.GetWealthScriptObject(this.Strategy),
                    GetSelectedScoreCard(sm)
                );
                this.executors[i].tse.ExternalSymbolRequested += this.OnLoadSymbol;
                this.executors[i].tse.ExternalSymbolFromDataSetRequested += this.OnLoadSymbolFromDataSet;
                this.results[i] = null;
                SynchronizeWealthScriptParameters(this.executors[i].ws, this.WealthScript);
            });
            this.paramValues = this.WealthScript.Parameters
                .Select(p =>
                {
                    // set the value to start
                    p.Value = p.Start;
                    return CopyParameter(p);
                })
                .ToList();

            //extract all data set bars
            foreach (var symbol in tse.DataSet.Symbols)
            {
                var bars = tse.BarsLoader.GetData(tse.DataSet, symbol);
                dataSetBars.Add(symbol, bars);
            }
        }

        /// <summary>
        /// Implements parallel runs - each logical "next run" results in multiple parallel runs
        /// </summary>
        public override bool NextRun(SystemPerformance sp, OptimizationResult or)
        {
            if (!this.supported)
                return false;

            this.countDown.Reset();
            for (int i = 0; i < numThreads; i++)
            {
                if (SetNextRunParameters())
                {
                    for (int j = 0; j < this.paramValues.Count; j++)
                        this.executors[i].ws.Parameters[j].Value = this.paramValues[j].Value;

                    ThreadPool.QueueUserWorkItem((e) =>
                    {
                        var threadNum = (int)e;
                        for (int retry = 0; ;)
                        {
                            try
                            {
                                //lock (this)   //for debug
                                {
                                    ExecuteOne(threadNum);

                                    if (threadNum == 0)
                                    {
                                        // have the main thread display this result in the result list UI
                                        for (int k = 0; k < this.executors[threadNum].ws.Parameters.Count; k++)
                                            this.WealthScript.Parameters[k].Value = this.executors[threadNum].ws.Parameters[k].Value;
                                        //TODO
                                        //здесь вроде как мы ставим параметры для исполнения скрипта главным потоком
                                        //кажется это дублирующая нагрузка после выполнения скрипта на тех же параметрах в ExecuteOne
                                    }
                                    else
                                    {
                                        // have this thread display this result in the result list UI
                                        AddResultsToUI(threadNum);
                                    }
                                    this.countDown.Signal();
                                    break;
                                }
                            }
                            catch (Exception)
                            {
                                retry++;
                                if (retry < 10)
                                    continue;
                                // couldn't get results for this run
                                this.results[threadNum] = null;
                                this.countDown.Signal();
                                break;
                            }
                        }
                    }, i);
                }
                else
                {
                    // this is the last run
                    this.supported = false;
                    for (int j = i; j < numThreads; j++)
                    {
                        this.results[j] = null;
                        this.countDown.Signal();
                    }
                    if (i == 0)
                        return false; // catch the boundary condition
                    break;
                    //вот здесь как будто бы можно заранее выйти из цикла тредов, не освободив полностью countdown
                    //если в последнем трае необработанных параметров осталось меньше чем число тредов
                }
            }
            this.countDown.Wait();
            return true;
        }

        private void ExecuteOne(int sys)
        {
            var ws = this.executors[sys].ws;
            //var ts = this.executors[sys].tse;
            var allBars = dataSetBars.Values.ToList();

            //new executer for each run - most valuable fix
            //TODO: тут рефлексия на рефлексии при каждом запуске, нужно оптимизировать
            //TODO: с using получились совсем иные результаты
            var ts = CreateExecutor(this.Strategy, GetPositionSize(this.WealthScript), settingsManager, this.WealthScript);
            ts.Execute(new Strategy(), ws, null, allBars);
            this.results[sys] = ts.Performance;
        }

        /// <summary>
        /// Adds a single set of results to the UI
        /// </summary>
        private void AddResultsToUI(int index)
        {
            ListViewItem row = new ListViewItem();
            row.SubItems[0].Text = this.WealthScript.Bars.Symbol;
            for (int i = 0; i < this.paramValues.Count; i++)
                row.SubItems.Add(this.executors[index].ws.Parameters[i].Value.ToString());
            this.executors[index].ss.PopulateScorecard(row, this.results[index]);

            var optimizationResultListView = (ListView)((TabControl)((UserControl)this.Host).Controls[0]).TabPages[1].Controls[0];
            optimizationResultListView.Invoke(new Action(() => optimizationResultListView.Items.Add(row)));
        }

        /// <summary>
        /// Synchronizes strategy parameters between scripts
        /// </summary>
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
                old.Name,
                old.Value,
                old.Start,
                old.Stop,
                old.Step,
                old.Description)
            {
                DefaultValue = old.DefaultValue
            };
        }

        /// <summary>
        /// Increments strategy parameter values for the next optimization run based on exhaustive optimization
        /// </summary>
        private bool SetNextRunParameters() => SetNextRunParameters(0);

        /// <summary>
        /// Increments strategy parameter values for the next optimization run based on exhaustive optimization - recursive implementation
        /// </summary>
        private bool SetNextRunParameters(int currentParam)
        {
            if (currentParam >= paramValues.Count)
                return false; // we're done

            paramValues[currentParam].Value += paramValues[currentParam].Step;
            //TODO: тут при неточном сложении даблов можно выйти за границу необсчитав последний параметр

            if ((paramValues[currentParam].Value > paramValues[currentParam].Stop && 
                paramValues[currentParam].Step > 0) 
                ||
                (paramValues[currentParam].Value < paramValues[currentParam].Stop && 
                paramValues[currentParam].Step < 0))
            {
                paramValues[currentParam].Value = paramValues[currentParam].Start;
                return SetNextRunParameters(currentParam + 1);
            }
            return true;
        }

        /// <summary>
        /// Creates a trading executor to be used for optimization runs
        /// </summary>
        private static TradingSystemExecutor CreateExecutor(Strategy strategy, PositionSize ps, SettingsManager settings, WealthScript ws)
        {
            FundamentalsLoader fundamentalsLoader = new FundamentalsLoader();
            fundamentalsLoader.DataHost = MainModuleInstance.GetDataSources();
            BarsLoader barsLoader = new BarsLoader();
            barsLoader.DataHost = fundamentalsLoader.DataHost;

            // this code is based on the PortfolioEquityEx from Community.Components 
            TradingSystemExecutor executor = new TradingSystemExecutor();

            // Store user-selected position size GUI dialog settings into a variable
            executor.PosSize = ps;
            if (settings.Settings.Count > 0)
            {
                executor.ApplyCommission = settings.Get("ApplyCommissions", false);
                executor.ApplyInterest = settings.Get("ApplyInterest", false);
                executor.ApplyDividends = settings.Get("ApplyDividends", false);
                executor.CashRate = settings.Get("CashRate", 0d);
                executor.EnableSlippage = settings.Get("EnableSlippage", false);
                executor.LimitDaySimulation = settings.Get("LimitDaySimulation", false);
                executor.LimitOrderSlippage = settings.Get("LimitOrderSlippage", false);
                executor.MarginRate = settings.Get("MarginRate", 0d);
                executor.NoDecimalRoundingForLimitStopPrice = settings.Get("NoDecimalRoundingForLimitStopPrice", false);
                executor.PricingDecimalPlaces = settings.Get("PricingDecimalPlaces", 2);
                executor.ReduceQtyBasedOnVolume = settings.Get("ReduceQtyBasedOnVolume", false);
                executor.RedcuceQtyPct = settings.Get("ReduceQtyPct", 0d);
                executor.RoundLots = settings.Get("RoundLots", false);
                executor.RoundLots50 = settings.Get("RoundLots50", false);
                executor.SlippageTicks = settings.Get("SlippageTicks", 0);
                executor.SlippageUnits = settings.Get("SlippageUnits", 0d);
                executor.WorstTradeSimulation = settings.Get("WorstTradeSimulation", false);
                executor.FundamentalsLoader = fundamentalsLoader;

                var parentExecutor = ExtractExecutor(ws);
                executor.BarsLoader = parentExecutor.BarsLoader;
                executor.DataSet = parentExecutor.DataSet;

                string commissionOption = settings.Get("Commission", string.Empty);
                if (!string.IsNullOrEmpty(commissionOption))
                {
                    AssemblyLoader assemblyLoader = new AssemblyLoader();
                    assemblyLoader.Path = Application.StartupPath;
                    assemblyLoader.BaseClass = "Commission";
                    Commission commission = null;
                    string selectedCommission = null;
                    AssemblyName whichAssembly = null;
                    Type targetType = null;
                    if (assemblyLoader.Assemblies.Count > 0)
                    {
                        foreach (Assembly asm in assemblyLoader.Assemblies)
                        {
                            foreach (Type type in assemblyLoader.TypesInAssembly(asm))
                            {
                                if (type.Name.Trim() == commissionOption.Trim())
                                {
                                    selectedCommission = type.Name;
                                    whichAssembly = asm.GetName();
                                    targetType = type;
                                    break;
                                }
                            }
                        }
                    }
                    if ((selectedCommission != null) & (whichAssembly != null))
                    {
                        Assembly asm = Assembly.Load(whichAssembly);
                        commission = (Commission)asm.CreateInstance(whichAssembly.Name + "." + targetType.Name);
                    }
                    if (commission != null)
                        executor.Commission = commission;
                }
            }
            return executor;
        }

        private static TradingSystemExecutor ExtractExecutor(WealthScript script)
        {
            var type = script.GetType();
            while (type != typeof(WealthScript))
                type = type.BaseType;

            var tsField = type
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .First(x => x.FieldType == typeof(TradingSystemExecutor));
            var tsExecutor = tsField.GetValue(script) as TradingSystemExecutor;
            return tsExecutor;
        }

        /// <summary>
        /// Returns the PositionSize object of a Strategy being executed
        /// </summary>
        private static PositionSize GetPositionSize(WealthScript wsObj)
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form.Name == "ChartForm")
                {
                    WealthScript ws = (WealthScript)form.GetType().GetProperty("WealthScript").GetValue(form, null);
                    if ((ws != null) && (ws.Equals(wsObj)))
                    {
                        PropertyInfo pi = form.GetType().GetProperty("PositionSize");
                        PositionSize ps = (PositionSize)pi.GetValue(form, null);
                        return ps;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a new instance of the configured scorecard
        /// </summary>
        private static StrategyScorecard GetSelectedScoreCard(SettingsManager sm)
        {
            if (sm.Settings.ContainsKey("Optimization.Scorecard"))
            {
                if (string.CompareOrdinal(sm.Settings["Optimization.Scorecard"], "Basic Scorecard") == 0)
                    return new BasicScorecard();
            }
            return new ExtendedScorecard();
        }
    }
}