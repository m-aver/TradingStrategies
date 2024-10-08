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

//перфоманс
//создание экзекутора через CreateExecutor занимает чуть больше времени чем выполнение скрипта (>20-30%) (много рефлексии, обязательно оптимизировать)
//при этом в параллельном режиме создание экзекутора и выполнение скрипта занимает в 3 раза больше времени, чем в последовательном (цикл в одном треде)
//100/70мс в последовательном и 300/200мс в параллельном
//в последовательном режиме с множеством тредов и lock цифры примерно такие же как с циклом, мб чуть больше
//в параллельном главный поток висит окло 500 мс на блокировке, в момент запуска тредов ЦП под 100%, пожалуй все треды действительно идут параллельно
//возможно где-то в недрах велслаба лочимся на каких-нибудь блокировках, поэтому такая разница между режимами 
//при добавлении фейковой нагрузки через Busy, утилизация ЦП поперла вверх, хотя не всегда в момент нагрузки была 100. очень похоже что все таки где-то внутри есть блокировки на которых скрипт троттлится (lock нагрузку не жрет)
//выполнение метода AddResultsToUi происходит мгновенно, тут оптимизация не нужна

//точки останова в коде стратегии очень сильно замедляют дебаг

//странный эффект, с CopyExecutor видно про стопвотчу что создание экзекутора стало занимать раза в 2 меньше времени
//но общее время выполнения осталось таким же (1:10)
//мб сборщик мусора замедляет перфоманс

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
        private TradingSystemExecutor parentExecutor;

        //for watches
        private int mainThread => numThreads + 1 - 1;
        private int currentRun = 0;
        private Stopwatch mainWatch = new Stopwatch();
        private List<(string stage, long milliseconds)>[] perfomance;

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
            //parentExecutor = ExtractExecutor(this.WealthScript);
            //parentExecutor = CopyExecutor(ExtractExecutor(this.WealthScript));
            //parentExecutor = CopyExecutor(CreateExecutor(this.Strategy, ps, sm, this.WealthScript));
            parentExecutor = CreateExecutor(this.Strategy, ps, sm, this.WealthScript);

            perfomance = new List<(string, long)>[numThreads + 1].Select(x => new List<(string, long)>()).ToArray();  //with main thread
        }

        private void Busy()
        {
            var start = DateTime.Now;
            var end = start + TimeSpan.FromSeconds(1);
            while (DateTime.Now < end) { }
        }

        public override bool NextRun(SystemPerformance sp, OptimizationResult or)
        {
            perfomance[mainThread].Add(($"all_end_{currentRun}", mainWatch.ElapsedMilliseconds));

            currentRun++;
            mainWatch.Restart();

            var x = NextRunInternal(sp, or);

            mainWatch.Restart();
            perfomance[mainThread].Add(($"all_start_{currentRun}", mainWatch.ElapsedMilliseconds));

            return x;
        }

        /// <summary>
        /// Implements parallel runs - each logical "next run" results in multiple parallel runs
        /// </summary>
        public bool NextRunInternal(SystemPerformance sp, OptimizationResult or)
        {
            if (!this.supported)
                return false;

            mainWatch.Restart();

            this.countDown.Reset();
            for (int i = 0; i < numThreads; i++)
            {
                if (SetNextRunParameters())
                {
                    for (int j = 0; j < this.paramValues.Count; j++)
                        this.executors[i].ws.Parameters[j].Value = this.paramValues[j].Value;

                    ThreadPool.QueueUserWorkItem((e) =>
                    {
                        //Busy();
                        var watch = new Stopwatch();
                        try
                        {
                            watch.Start();

                            var threadNum = (int)e;
                            for (int retry = 0; ;)
                            {
                                try
                                {
                                    //lock (this)   //for debug
                                    {
                                        //Busy();
                                        ExecuteOne(threadNum);

                                        if (threadNum == 0)
                                        {
                                            // have the main thread display this result in the result list UI
                                            for (int k = 0; k < this.executors[threadNum].ws.Parameters.Count; k++)
                                                this.WealthScript.Parameters[k].Value = this.executors[threadNum].ws.Parameters[k].Value;
                                            //TODO
                                            //здесь вроде как мы ставим параметры для исполнения скрипта главным потоком
                                            //кажется это дублирующая нагрузка после выполнения скрипта на тех же параметрах в ExecuteOne
                                            // --
                                            //кажется можно еще в два раза увеличить перфоманс, если не ждать треды
                                            //чтобы родные запуски за пределами оптимизера выполнялись параллельно тредам
                                        }
                                        else
                                        {
                                            var uiWatch = new Stopwatch();
                                            uiWatch.Start();
                                            // have this thread display this result in the result list UI
                                            AddResultsToUI(threadNum);
                                            perfomance[threadNum].Add(($"ui_{currentRun}", uiWatch.ElapsedMilliseconds));
                                        }
                                        this.countDown.Signal();
                                        //Busy();
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
                        }
                        finally
                        {
                            watch.Stop();
                            perfomance[(int)e].Add(($"all_{currentRun}", watch.ElapsedMilliseconds));
                            //Busy();
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

            perfomance[mainThread].Add(($"all_runThreads_{currentRun}", mainWatch.ElapsedMilliseconds));
            mainWatch.Restart();

            this.countDown.Wait();

            perfomance[mainThread].Add(($"all_countDown_{currentRun}", mainWatch.ElapsedMilliseconds));
            return true;
        }

        private void ExecuteOne(int sys)
        {
            var watch = new Stopwatch();

            var ws = this.executors[sys].ws;
            //var ts = this.executors[sys].tse;
            var allBars = dataSetBars.Values.ToList();

            //new executer for each run - most valuable fix
            //TODO: тут рефлексия на рефлексии при каждом запуске, нужно оптимизировать
            //TODO: с using получились совсем иные результаты

            watch.Start();
            //var ts = CreateExecutor(this.Strategy, GetPositionSize(this.WealthScript), settingsManager, this.WealthScript);
            var ts = CopyExecutor(parentExecutor);
            //при последовательной обработке с CopyExecutor нет проблем, при параллельной результаты хаотичные
            //но число сделок одинаково, видимо при расчете результатов где-то возникают коллизии

            //мб попробовать бары копировать
            //хотя CreateExecutor же с исходными работает
            //но может он лочиться при выполнении перестанет хотябы

            perfomance[sys].Add(($"executor_{currentRun}", watch.ElapsedMilliseconds));

            watch.Restart();
            ts.Execute(new Strategy(), ws, null, allBars);
            perfomance[sys].Add(($"script_{currentRun}", watch.ElapsedMilliseconds));

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

            var current = paramValues[currentParam];

            current.Value += current.Step;

            if ((current.Value >= (current.Stop + current.Step) &&
                current.Step > 0)
                ||
                (current.Value <= (current.Stop - current.Step) &&
                current.Step < 0))
            {
                current.Value = current.Start;
                return SetNextRunParameters(currentParam + 1);
            }
            return true;
        }

        private TradingSystemExecutor CopyExecutor(TradingSystemExecutor source)
        {
            //var dataHost = source.FundamentalsLoader.DataHost;
            //var barsLoader = new BarsLoader()
            //{
            //    DataHost = dataHost,
            //};

            //эта штука внутри сильно тормозит на рефлексии
            //вроде обычно копирование поле из source дает такие же результаты, но работает быстрее
            //var fundamentalsLoader = new FundamentalsLoader()
            //{
            //    DataHost = dataHost
            //};

            //FundamentalsLoader fundamentalsLoader = new FundamentalsLoader();
            //fundamentalsLoader.DataHost = MainModuleInstance.GetDataSources();

            var target = new TradingSystemExecutor()
            {
                ApplyCommission = source.ApplyCommission,
                ApplyInterest = source.ApplyInterest,
                ApplyDividends = source.ApplyDividends,
                CashRate = source.CashRate,
                EnableSlippage = source.EnableSlippage,
                LimitDaySimulation = source.LimitDaySimulation,
                LimitOrderSlippage = source.LimitOrderSlippage,
                MarginRate = source.MarginRate,
                NoDecimalRoundingForLimitStopPrice = source.NoDecimalRoundingForLimitStopPrice,
                PricingDecimalPlaces = source.PricingDecimalPlaces,
                ReduceQtyBasedOnVolume = source.ReduceQtyBasedOnVolume,
                RedcuceQtyPct = source.RedcuceQtyPct,
                RoundLots = source.RoundLots,
                RoundLots50 = source.RoundLots50,
                SlippageTicks = source.SlippageTicks,
                SlippageUnits = source.SlippageUnits,
                WorstTradeSimulation = source.WorstTradeSimulation,

                FundamentalsLoader = source.FundamentalsLoader,
                BarsLoader = source.BarsLoader,
                DataSet = source.DataSet,
                Commission = source.Commission,
                //PosSize = source.PosSize,
                PosSize = CopyPositionSize(source.PosSize),

                BenchmarkBuyAndHoldON = false,
                    
            };

            return target;
        }

        //с этой штукой CopyExecutor перестал показывать хаотичные результаты
        //и теперь работает также как и с CreateExecutor
        //не удивительно, при каждом вызове SetShareSize с последующим входом в позицию через WealthScript перезаписываются свойства этого объекта PositionSize внутри TradingSystemExecutor, и высчитывается число Shares на этой Position
        //только вот CopyExecutor как будто работает даже медленее чем CreateExecutor
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

        /// <summary>
        /// Creates a trading executor to be used for optimization runs
        /// </summary>
        private static TradingSystemExecutor CreateExecutor(Strategy strategy, PositionSize ps, SettingsManager settings, WealthScript ws)
        {
            FundamentalsLoader fundamentalsLoader = new FundamentalsLoader();
            fundamentalsLoader.DataHost = MainModuleInstance.GetDataSources();
            //BarsLoader barsLoader = new BarsLoader();
            //barsLoader.DataHost = fundamentalsLoader.DataHost;

            var parentExecutor = ExtractExecutor(ws);
            //добавление контейнера уменьшило нагрузку на цп и очень сильно увеличило потребление оперативы, но результаты оказались такие же рандомные (для CopyExecutor)
            //new TradingSystemExecutor(parentExecutor.Container);

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