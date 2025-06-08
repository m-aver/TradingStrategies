using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

/// <summary>
/// Takes every N (sparce factor) iteration over source iterator with starting from offset
/// </summary>
internal class SparseParametersIterator : IStrategyParametersIterator
{
    private readonly IStrategyParametersIterator _sourceIterator;
    private readonly int _offset;
    private readonly int _sparseFactor;

    private int _current = 0;
    private int _currentFromOffset = 0;

    public SparseParametersIterator(IStrategyParametersIterator sourceIterator, int offset, int sparseFactor)
    {
        _sourceIterator = sourceIterator;
        _offset = offset;
        _sparseFactor = sparseFactor;
    }

    public IEnumerable<StrategyParameter> CurrentParameters => _sourceIterator.CurrentParameters;

    public bool MoveNext()
    {
        while (_current < _offset)
        {
            if (!_sourceIterator.MoveNext())
            {
                return false;
            }

            _current++;
        }

        while (_currentFromOffset % _sparseFactor != 0)
        {
            if (!_sourceIterator.MoveNext())
            {
                return false;
            }

            _currentFromOffset++;
        }

        _currentFromOffset++;

        return _sourceIterator.MoveNext();
    }

    public void Reset() => _sourceIterator.Reset();
}
