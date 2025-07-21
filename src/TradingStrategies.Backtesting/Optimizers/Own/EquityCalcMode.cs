namespace TradingStrategies.Backtesting.Optimizers.Own;

/// <summary>
/// Defines mode of equity and cash curves calculation
/// </summary>
public enum EquityCalcMode
{
    /// <summary>
    /// Calculate equity and cash dataseries for every bar in dataset. 
    /// Low perfomance. High accuracy. 
    /// </summary>
    /// <remarks>
    /// Perfomance depends on bars number.
    /// </remarks>
    Full,

    /// <summary>
    /// Calculate equity and cash dataseries only when there are active positions on bar. 
    /// Medium perfomance. Medium accuracy.
    /// </summary>
    /// <remarks>
    /// Perfomance depends on positions number and their life duration. 
    /// It may have reduced accuracy for some metrics, e.g. based on log-error. 
    /// Equity on skipped bars has values of last calculated value, this is naturally deduplication.
    /// Additionaly there are appended starting and ending points to specify boundaries of initial period.
    /// </remarks>
    Sampled,

    /// <summary>
    /// Calculate equity and cash dataseries only when positions were closed. 
    /// High perfomance. Low accuracy.
    /// </summary>
    /// <remarks>
    /// Perfomance depends on positions number. 
    /// It have reduced accuracy for most of metrics (based on log-error, month-returns, drawdown, etc). 
    /// Cash on skipped bars has values of last calculated value, this is naturally deduplication.
    /// Equity has deduplicated step curve, that can be useful due to elimination of price spikes and noise.
    /// Additionaly there are appended starting and ending points to specify boundaries of initial period.
    /// </remarks>
    Closed,
}
