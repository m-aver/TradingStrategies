﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Utility;
using TradingStrategies.Backtesting.Utility;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers;

/// <summary>
/// Implements multithreaded optimization
/// </summary>
/// <remarks>
/// Enhanced perfomance of <see cref="ParallelExhaustiveOptimizer"/> but may have broken progress bar.
/// Specifically with optimization run filtration via <see cref="Strategies.FiltrationStrategyDecorator"/>
/// </remarks>
public class ParallelExhaustiveOptimizerEx : OptimizerBase
{
    /// <summary>
    /// Represents scope of data for one separate optimization run
    /// </summary>
    private class ExecutionScope
    {
        public TradingSystemExecutor Executor { get; }
        public WealthScript Script { get; }
        public StrategyScorecard Scorecard { get; }
        public SystemPerformance Result { get; set; }
        public Strategy Strategy { get; }
        public List<Bars> BarsSet { get; }
        public List<ListViewItem> ResultRows { get; }
        public IStrategyParametersIterator ParametersIterator { get; }
        public List<StrategyParameter> ParameterValues { get; }

        public ExecutionScope(
            TradingSystemExecutor executor,
            WealthScript script,
            StrategyScorecard scorecard,
            Strategy strategy,
            List<Bars> barsSet,
            List<ListViewItem> resultRows,
            IStrategyParametersIterator parametersIterator)
        {
            Executor = executor;
            Script = script;
            Scorecard = scorecard;
            Strategy = strategy;
            BarsSet = barsSet;
            ResultRows = resultRows;
            ParametersIterator = parametersIterator;
            ParameterValues = parametersIterator.CurrentParameters.ToList();
        }
    }

    private int numThreads;
    private ExecutionScope[] executors;
    private Task[] executions;
    private List<StrategyParameter> paramValues;
    private IStrategyParametersIterator parametersIterator;
    private Dictionary<string, Bars> dataSetBars;
    private TradingSystemExecutor parentExecutor;

    public override string FriendlyName => "Parallel Optimizer (Exhaustive) Enhanced";
    public override string Description => "Enhanced version of Exhaustive Parallel Optimizer. May have inaccurate progress bar";

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

    public override void Initialize()
    {
        base.Initialize();

        numThreads = Environment.ProcessorCount;
    }

    public override void RunCompleted(OptimizationResultList results)
    {
        var isCompleted = Task.WaitAll(executions, 1000);

        if (!isCompleted)
        {
            var message = "Not all threads have completed their runs. The result set will not be complete";
            MessageBox.Show(message);
        }

        base.RunCompleted(results);

        PopulateUI();
        FullCollect();
    }

    public override void FirstRun()
    {
        base.FirstRun();

        FullCollect();

        parentExecutor = WealthScriptHelper.ExtractExecutor(WealthScript)!;
        if (parentExecutor == null)
        {
            var message = $"Cannot load executor, you must run strategy firstly on that dataset everywhere";
            Debug.Print(message);
            MessageBox.Show(message);
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
            return;
        }

        // check if this strategy can be run by this optimizer
        var strategyManager = new StrategyManager();
        var testScript = strategyManager.GetWealthScriptObject(Strategy);
        var testExecutor = CopyExecutor(parentExecutor);
        var testStrategy = CopyStrategy(Strategy);
        var testBars = dataSetBars.Values.Select((x, i) => x.Prepare(i + 1)).ToList();

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
            return;
        }

        var iterators = CreateSplittedIterators();

        //initialize parallel executors
        executors = new ExecutionScope[numThreads - 1];

        var runs = (int)NumberOfRuns;
        Parallel.For(0, numThreads - 1, i =>
        {
            var offset = i * dataSetBars.Values.Count;
            executors[i] = new ExecutionScope(
                CopyExecutor(parentExecutor),
                strategyManager.GetWealthScriptObject(Strategy),
                CopySelectedScoreCard(),
                CopyStrategy(Strategy),
                dataSetBars.Values.Select((x, j) => x.Prepare(j + 1 + offset)).ToList(),
                new List<ListViewItem>(runs),
                iterators[i]
            );
            SynchronizeWealthScriptParameters(executors[i].Script, WealthScript);
        });

        //initialize parameters
        parametersIterator = iterators.Last();
        paramValues = parametersIterator.CurrentParameters.ToList();

        //run parallel optimization threads
        executions = executors.Select(RunScope).ToArray();
    }

    private Task RunScope(ExecutionScope execution)
    {
        return Task.Factory.StartNew(
            action: state => RunScopeInternal((ExecutionScope)state, CancellationTokenSource.Token),
            state: execution,
            cancellationToken: CancellationTokenSource.Token,
            creationOptions: TaskCreationOptions.LongRunning,
            scheduler: TaskScheduler.Default);
    }

    private static void RunScopeInternal(ExecutionScope executors, CancellationToken cancellationToken)
    {
        while (executors.ParametersIterator.MoveNext())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var parameterValues = executors.ParameterValues;

            for (int j = 0; j < parameterValues.Count; j++)
            {
                executors.Script.Parameters[j].Value = parameterValues[j].Value;
            }

            try
            {
                ExecuteOne(executors);

                PrepareResultsToUI(executors);
            }
            catch
            {
                executors.Result = null;
            }
            finally
            {
                executors.Executor.Clear();
            }
        }
    }

    public override bool NextRun(SystemPerformance sp, OptimizationResult or)
    {
        if (parametersIterator.MoveNext())
        {
            for (int j = 0; j < paramValues.Count; j++)
            {
                WealthScript.Parameters[j].Value = paramValues[j].Value;
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteOne(ExecutionScope executors)
    {
        var ts = executors.Executor;

        ts.Initialize();
        ts.Execute(executors.Strategy, executors.Script, null, executors.BarsSet);

        executors.Result = ts.Performance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PrepareResultsToUI(ExecutionScope executors)
    {
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
    }

    private void PopulateUI()
    {
        var optimizationResultListView = OptimizationFormExtractor.ExtractOptimizationResultListView(this);

        var rows = executors.SelectMany(x => x.ResultRows).ToArray();

        if (optimizationResultListView.InvokeRequired)
        {
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

    private IStrategyParametersIterator CreateParametersIterator(bool fromStart = true)
    {
        var parameters = WealthScript.Parameters.Select(CopyParameter).ToArray();
        var iterator = new StrategyParametersIterator(parameters);
        if (fromStart)
            iterator.Reset();
        return iterator;
    }

    private IList<IStrategyParametersIterator> CreateSplittedIterators()
    {
        return Enumerable
            .Range(0, numThreads)
            .Select(i => new SparseParametersIterator(CreateParametersIterator(), i, numThreads))
            .Cast<IStrategyParametersIterator>()
            .ToList();
    }

    private StrategyScorecard CopySelectedScoreCard()
    {
        var scorecard = ScorecardProvider.GetSelectedScorecard();
        scorecard = (StrategyScorecard)Activator.CreateInstance(scorecard.GetType());
        return scorecard;
    }

    private static void SynchronizeWealthScriptParameters(WealthScript wsTarget, WealthScript wsSource) =>
        ParallelExhaustiveOptimizer.SynchronizeWealthScriptParameters(wsTarget, wsSource);
    private static StrategyParameter CopyParameter(StrategyParameter old) => ParallelExhaustiveOptimizer.CopyParameter(old);
    private static TradingSystemExecutor CopyExecutor(TradingSystemExecutor source) => ParallelExhaustiveOptimizer.CopyExecutor(source);
    private static PositionSize CopyPositionSize(PositionSize source) => ParallelExhaustiveOptimizer.CopyPositionSize(source);
    private static Strategy CopyStrategy(Strategy source) => ParallelExhaustiveOptimizer.CopyStrategy(source);
    private static void FullCollect() => ParallelExhaustiveOptimizer.FullCollect();
}