using System.Buffers;
using System.Collections;

namespace TradingStrategies.Backtesting.Utility;

//to manage buffer lifetime
internal struct BufferedIterator<T> : IEnumerator<T> 
    where T : notnull
{
    private readonly T[] _buffer;
    private readonly IEnumerator<T> _enumerator;

    public BufferedIterator(int bufferLenght, Func<T[], IEnumerable<T>> payloadBuilder)
    {
        _buffer = ArrayPool<T>.Shared.Rent(bufferLenght);
        _enumerator = payloadBuilder(_buffer).GetEnumerator();
    }

    public T Current => _enumerator.Current;
    object IEnumerator.Current => Current;
    public bool MoveNext() => _enumerator.MoveNext();
    public void Reset() => _enumerator.Reset();

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_buffer);
        _enumerator.Dispose();
    }
}
