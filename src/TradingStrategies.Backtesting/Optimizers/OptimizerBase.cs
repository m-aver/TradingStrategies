using Fidelity.Components;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Scorecards;
using TradingStrategies.Backtesting.Optimizers.Utility;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers;

public abstract class OptimizerBase : Optimizer
{
    protected IScorecardProvider ScorecardProvider { get; private set; }
    protected CancellationTokenSource CancellationTokenSource { get; private set; }

    /// <summary>
    /// Initializes the optimizer. Called when optimization method has been selected. Run once for multiple optimization sessions.
    /// </summary>
    public override void Initialize()
    {
        var settingsManager = new SettingsManager();
        settingsManager.RootPath = Application.UserAppDataPath + Path.DirectorySeparatorChar + "Data";
        settingsManager.FileName = "WealthLabConfig.txt";
        settingsManager.LoadSettings();

        ScorecardProvider = ScorecardProviderFactory.Create(settingsManager, this);
    }

    /// <summary>
    /// The very first run for this optimization. Sets up everything for the entire optimization. Run once for one optimization session.
    /// </summary>
    public override void FirstRun()
    {
        //cancellation
        CancellationTokenSource = new();
        var cancelOptimizationButton = OptimizationFormExtractor.ExtractCancellationButton(this);
        cancelOptimizationButton.Click += (_, _) => CancellationTokenSource.Cancel();
    }
}
