using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using WealthLab;
using WealthLab.Visualizers;
using Fidelity.Components;
using System.Linq;
using System.Collections.Concurrent;

namespace WealthLab.Optimizers.Community
{
    static class MainModuleInstance
    {
        private static object _instance;
        private static PropertyInfo _dataSources;

        /// <summary>
        /// Ctor
        /// </summary>
        static MainModuleInstance()
        {
            Initialize();
        }

        /// <summary>
        /// Get Wealth-Lab Dev/Pro exe assembly
        /// </summary>
        /// <returns></returns>
        private static Assembly GetWLAssembly()
        {
            Assembly wl = null;

            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.FullName.Contains("WealthLabDev") || a.FullName.Contains("WealthLabPro"))
                {
                    wl = a;
                    break;
                }
            }

            if (wl == null) throw new Exception("Wealth-Lab not found.");

            return wl;
        }

        /// <summary>
        /// Set Main Module Instance
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static void SetMainModuleInstance()
        {
            Type mainModuleType = null;

            foreach (Type t in GetWLAssembly().GetTypes())
            {
                Type[] interfaces = t.GetInterfaces();

                bool wlpDetected = false;
                int c = 0;
                foreach (Type i in interfaces)
                {
                    if (i == typeof(WealthLab.IAuthenticationHost) ||
                        (i == typeof(WealthLab.IConnectionStatus)) ||
                        (i == typeof(WealthLab.IMenuItemAdder)))
                        c++;

                    if (c >= 3)
                        wlpDetected = true;
                }
                if (t.FullName == "WealthLabPro.MainModule" || wlpDetected)
                {
                    mainModuleType = t;
                    break;
                }
            }

            if (mainModuleType == null) throw new Exception("MainModule not found.");

            FieldInfo fiInstance = null;

            foreach (FieldInfo field in mainModuleType.GetFields())
            {
                if (field.FieldType == mainModuleType)
                {
                    fiInstance = field;
                    break;
                }
            }

            if (fiInstance == null) throw new Exception("MainModule.Instance not found.");

            _instance = fiInstance.GetValue(null);
        }

        /// <summary>
        /// Initialize
        /// </summary>
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
            foreach (PropertyInfo field in _instance.GetType().GetProperties())
            {
                if (field.PropertyType == typeof(DataSourceManager))
                {
                    _dataSources = field;
                }
            }

            if ((_dataSources == null)) throw new Exception("DataSourceManager field not found.");
        }

        /// <summary>
        /// DataSourceManager
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static DataSourceManager GetDataSources()
        {
            return (DataSourceManager)_dataSources.GetValue(_instance, new object[] { });
        }
    }

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
    public class ExhaustiveMultiThreadedOptimizer : Optimizer
    {
        private int numThreads;
        private Tuple<TradingSystemExecutor, WealthScript, StrategyScorecard>[] executors;
        private SystemPerformance[] results;
        private CountdownEvent countDown;
        private List<StrategyParameter> paramValues;
        private bool supported;
        private Dictionary<string, Bars> barsCache;

        private ConcurrentQueue<ParamInfo> paramInfos = new ConcurrentQueue<ParamInfo>();
        private Dictionary<string, Bars> dataSetBars = new Dictionary<string, Bars>();
        private SettingsManager settingsManager;

        //TODO
        public override void RunCompleted(OptimizationResultList results)
        {
            base.RunCompleted(results);

            var data = this.results.Where(x => x != null).Select((x, idx) => new ParamInfo()
            {
                NetProfit = x.Results.NetProfit,
            });
        }

        /// <summary>
        /// Returns the optimizer's description shown in the UI
        /// </summary>
        public override string Description
        {
            get { return "Parallel Optimizer (Exhaustive)"; }
        }

        /// <summary>
        /// Returns the optimizer's description shown in the UI
        /// </summary>
        public override string FriendlyName
        {
            get { return Description; }
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
                foreach (StrategyParameter p in WealthScript.Parameters)
                    runs *= NumberOfRunsPerParameter(p);
                runs = Math.Max(Math.Floor(runs / numThreads), 1);
                if (runs > int.MaxValue)
                    runs = int.MaxValue; // otherwise the progress bar is broken
                return runs;
            }
        }

        /// <summary>
        /// Returns the number of runs required for a single strategy parameter
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double NumberOfRunsPerParameter(StrategyParameter p)
        {
            if ((p.Start < p.Stop && p.Step > 0) || (p.Start > p.Stop && p.Step < 0))
                return Math.Max(Math.Floor((p.Stop - p.Start + p.Step) / p.Step), 1);
            return 1;
        }

        /// <summary>
        /// Callback for the executor to get external symbols
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="a"></param>
        private void OnLoadSymbol(Object sender, LoadSymbolEventArgs a)
        {
            if (barsCache.ContainsKey(a.Symbol))
                a.SymbolData = barsCache[a.Symbol];
            else
            {
                lock (this.WealthScript)
                {
                    // check no other thread got here first
                    if (!barsCache.ContainsKey(a.Symbol))
                    {
                        this.WealthScript.SetContext(a.Symbol, false);
                        barsCache[a.Symbol] = this.WealthScript.Bars;
                        this.WealthScript.RestoreContext();
                    }
                    a.SymbolData = barsCache[a.Symbol];
                }
            }
        }

        /// <summary>
        /// Callback for the executor to get external symbols
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="a"></param>
        private void OnLoadSymbolFromDataSet(Object sender, LoadSymbolFromDataSetEventArgs a)
        {
            if (barsCache.ContainsKey(a.Symbol))
                a.Bars = barsCache[a.Symbol];
            else
            {
                lock (this.WealthScript)
                {
                    // check no other thread got here first
                    if (!barsCache.ContainsKey(a.Symbol))
                    {
                        this.WealthScript.SetContext(a.Symbol, false);
                        barsCache[a.Symbol] = this.WealthScript.Bars;
                        this.WealthScript.RestoreContext();
                    }
                    a.Bars = barsCache[a.Symbol];
                }
            }
        }

        /// <summary>
        /// The very first run for this optimization.  Sets up everything for the entire optimization
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
            this.executors = new Tuple<TradingSystemExecutor, WealthScript, StrategyScorecard>[numThreads];
            Parallel.For(0, numThreads, i =>
            {
                this.executors[i] = new Tuple<TradingSystemExecutor, WealthScript, StrategyScorecard>(
                    CreateExecutor(this.Strategy, ps, sm, this.WealthScript),
                    stm.GetWealthScriptObject(this.Strategy),
                    GetSelectedScoreCard(sm));
                this.executors[i].Item1.ExternalSymbolRequested += this.OnLoadSymbol;
                this.executors[i].Item1.ExternalSymbolFromDataSetRequested += this.OnLoadSymbolFromDataSet;
                this.results[i] = null;
                SynchronizeWealthScriptParameters(this.executors[i].Item2, this.WealthScript);
            });
            this.paramValues = new List<StrategyParameter>(this.WealthScript.Parameters.Count);
            for (int i = 0; i < this.WealthScript.Parameters.Count; i++)
            {
                this.paramValues.Add(new StrategyParameter(
                    this.WealthScript.Parameters[i].Name,
                    this.WealthScript.Parameters[i].Start, // set the value to start
                    this.WealthScript.Parameters[i].Start,
                    this.WealthScript.Parameters[i].Stop,
                    this.WealthScript.Parameters[i].Step,
                    this.WealthScript.Parameters[i].Description));
                this.paramValues[i].DefaultValue = this.WealthScript.Parameters[i].DefaultValue;
            }
            for (int i = 0; i < this.paramValues.Count; i++)
                this.WealthScript.Parameters[i].Value = this.paramValues[i].Value;

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
        /// <param name="sp"></param>
        /// <param name="or"></param>
        /// <returns></returns>
        public override bool NextRun(SystemPerformance sp, OptimizationResult or)
        {
            if (!this.supported)
                return false;
            this.countDown.Reset();
            for (int i = 0; i < numThreads; i++)
            {
                if (GetNextRunParameters())
                {
                    for (int j = 0; j < this.paramValues.Count; j++)
                        this.executors[i].Item2.Parameters[j].Value = this.paramValues[j].Value;
                    ThreadPool.QueueUserWorkItem((e) =>
                    {
                        for (int retry = 0; ;)
                        {
                            try
                            {
                                //lock (this)   //for debug
                                {
                                    ExecuteOne((int)e);

                                    var curParams = this.executors[(int)e].Item2.Parameters;
                                    var info = new ParamInfo()
                                    {
                                        MaPeriod = curParams.Single(x => x.Name == "maPeriod").Value,
                                        MaPercent = curParams.Single(x => x.Name == "maPercent").Value,
                                        SigmoidOffset = curParams.Single(x => x.Name == "sigmoidOffset").Value,
                                        SigmoidStretch = curParams.Single(x => x.Name == "sigmoidStretch").Value,
                                        NetProfit = this.executors[(int)e].Item1.Performance.Results.NetProfit,
                                    };
                                    paramInfos.Enqueue(info);

                                if ((int)e == 0)
                                {
                                    // have the main thread display this result in the result list UI
                                    for (int k = 0; k < this.executors[(int)e].Item2.Parameters.Count; k++)
                                        this.WealthScript.Parameters[k].Value = this.executors[(int)e].Item2.Parameters[k].Value;
                                }
                                else
                                {
                                    // have this thread display this result in the result list UI
                                    AddResultsToUI((int)e);
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
                                this.results[(int)e] = null;
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
                }
            }
            this.countDown.Wait();
            return true;
        }

        private void ExecuteOne(int sys)
        {
            var ws = this.executors[sys].Item2;
            var ts = this.executors[sys].Item1;
            var allBars = dataSetBars.Select(x => x.Value).ToList();

            //new executer for each run - most valuable fix
            ts = CreateExecutor(this.Strategy, GetPositionSize(this.WealthScript), settingsManager, this.WealthScript);

            ts.Execute(new Strategy(), ws, null, allBars);
            this.results[sys] = ts.Performance;
        }

        /// <summary>
        /// Adds a single set of results to the UI
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddResultsToUI(int index)
        {
            ListViewItem lvi = new ListViewItem();
            lvi.SubItems[0].Text = this.WealthScript.Bars.Symbol;
            for (int i = 0; i < this.paramValues.Count; i++)
                lvi.SubItems.Add(this.executors[index].Item2.Parameters[i].Value.ToString());
            this.executors[index].Item3.PopulateScorecard(lvi, this.results[index]);
            ((ListView)((TabControl)((UserControl)this.Host).Controls[0]).TabPages[1].Controls[0]).Invoke(new Action(() =>
            ((ListView)((TabControl)((UserControl)this.Host).Controls[0]).TabPages[1].Controls[0]).Items.Add(lvi)));
        }

        /// <summary>
        /// Synchronizes strategy parameters between scripts
        /// </summary>
        /// <param name="wsTarget"></param>
        /// <param name="wsSource"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SynchronizeWealthScriptParameters(WealthScript wsTarget, WealthScript wsSource)
        {
            wsTarget.Parameters.Clear();
            for (int i = 0; i < wsSource.Parameters.Count; i++)
            {
                wsTarget.Parameters.Add(new StrategyParameter(
                    wsSource.Parameters[i].Name,
                    wsSource.Parameters[i].Value,
                    wsSource.Parameters[i].Start,
                    wsSource.Parameters[i].Stop,
                    wsSource.Parameters[i].Step,
                    wsSource.Parameters[i].Description));
                wsTarget.Parameters[i].DefaultValue = wsSource.Parameters[i].DefaultValue;
            }

        }

        /// <summary>
        /// Increments strategy parameter values for the next optimization run baesd on exhaustive optimization
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetNextRunParameters() { return GetNextRunParameters(0); }

        /// <summary>
        /// Increments strategy parameter values for the next optimization run baesd on exhaustive optimization - recursive implementation
        /// </summary>
        /// <returns></returns>
        private bool GetNextRunParameters(int currentParam)
        {
            if (currentParam >= paramValues.Count)
                return false; // we're done
            paramValues[currentParam].Value += paramValues[currentParam].Step;
            if ((paramValues[currentParam].Value > paramValues[currentParam].Stop
                && paramValues[currentParam].Step > 0) ||
                (paramValues[currentParam].Value < paramValues[currentParam].Stop
                && paramValues[currentParam].Step < 0))
            {
                paramValues[currentParam].Value = paramValues[currentParam].Start;
                return GetNextRunParameters(currentParam + 1);
            }
            return true;
        }

        /// <summary>
        /// Creates a trading executor to be used for optimization runs
        /// </summary>
        /// <param name="s"></param>
        /// <param name="sm"></param>
        /// <param name="ws"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TradingSystemExecutor CreateExecutor(Strategy s, PositionSize ps, SettingsManager sm, WealthScript ws)
        {
            FundamentalsLoader fundamentalsLoader = new FundamentalsLoader();
            fundamentalsLoader.DataHost = MainModuleInstance.GetDataSources();
            BarsLoader barsLoader = new BarsLoader();
            barsLoader.DataHost = fundamentalsLoader.DataHost;

            // this code is based on the PortfolioEquityEx from Community.Components 
            TradingSystemExecutor executor = new TradingSystemExecutor();

            // Store user-selected position size GUI dialog settings into a variable
            executor.PosSize = ps;
            if (sm.Settings.Count > 0)
            {
                executor.ApplyCommission = sm.Get("ApplyCommissions", false);
                executor.ApplyInterest = sm.Get("ApplyInterest", false);
                executor.ApplyDividends = sm.Get("ApplyDividends", false);
                executor.CashRate = sm.Get("CashRate", 0d);
                executor.EnableSlippage = sm.Get("EnableSlippage", false);
                executor.LimitDaySimulation = sm.Get("LimitDaySimulation", false);
                executor.LimitOrderSlippage = sm.Get("LimitOrderSlippage", false);
                executor.MarginRate = sm.Get("MarginRate", 0d);
                executor.NoDecimalRoundingForLimitStopPrice = sm.Get("NoDecimalRoundingForLimitStopPrice", false);
                executor.PricingDecimalPlaces = sm.Get("PricingDecimalPlaces", 2);
                executor.ReduceQtyBasedOnVolume = sm.Get("ReduceQtyBasedOnVolume", false);
                executor.RedcuceQtyPct = sm.Get("ReduceQtyPct", 0d);
                executor.RoundLots = sm.Get("RoundLots", false);
                executor.RoundLots50 = sm.Get("RoundLots50", false);
                executor.SlippageTicks = sm.Get("SlippageTicks", 0);
                executor.SlippageUnits = sm.Get("SlippageUnits", 0d);
                executor.WorstTradeSimulation = sm.Get("WorstTradeSimulation", false);
                executor.FundamentalsLoader = fundamentalsLoader;

                var parentExecutor = ExtractExecutor(ws);
                executor.BarsLoader = parentExecutor.BarsLoader;
                executor.DataSet = parentExecutor.DataSet;

                string CommissionOption = sm.Get("Commission", string.Empty);
                if (!string.IsNullOrEmpty(CommissionOption))
                {
                    AssemblyLoader al = new AssemblyLoader();
                    al.Path = Application.StartupPath;
                    al.BaseClass = "Commission";
                    Commission c = null;
                    string selectedCommission = null;
                    AssemblyName whichAssembly = null;
                    Type tt = null;
                    if (al.Assemblies.Count > 0)
                    {
                        foreach (System.Reflection.Assembly a in al.Assemblies)
                        {
                            foreach (System.Type t in al.TypesInAssembly(a))
                            {
                                if (t.Name.Trim() == CommissionOption.Trim())
                                {
                                    selectedCommission = t.Name;
                                    whichAssembly = a.GetName();
                                    tt = t;
                                    break;
                                }
                            }
                        }
                    }
                    if ((selectedCommission != null) & (whichAssembly != null))
                    {
                        Assembly asm = Assembly.Load(whichAssembly);
                        c = (Commission)asm.CreateInstance(whichAssembly.Name + "." + tt.Name);
                    }
                    if (c != null)
                        executor.Commission = c;
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
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PositionSize GetPositionSize(WealthScript wsObj)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f.Name == "ChartForm")
                {
                    WealthScript ws = (WealthScript)f.GetType().GetProperty("WealthScript").GetValue(f, null);
                    if ((ws != null) && (ws.Equals(wsObj)))
                    {
                        PropertyInfo p = f.GetType().GetProperty("PositionSize");
                        PositionSize ps = (PositionSize)p.GetValue(f, null);
                        return ps;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a new instance of the configured scorecard
        /// </summary>
        /// <param name="sm"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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