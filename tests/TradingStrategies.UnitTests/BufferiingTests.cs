using TradingStrategies.Backtesting.Utility;

namespace TradingStrategies.UnitTests;

public partial class BufferiingTests
{
    [Theory]
    [MemberData(nameof(GetTestData))]
    public void ToBuffer_Iterate_Success(IEnumerable<double> source, int bufferLength)
    {
        //arrange
        var buffer = new double[bufferLength];

        //act
        var buffered = source.ToBuffer(buffer);

        //assert
        Assert.Equal(source, buffered);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    public void ToBuffer_MultiComplexIterate_Success(IEnumerable<double> source, int bufferLength)
    {
        //arrange
        var buffer = new double[bufferLength];

        //act
        var buffered = source.ToBuffer(buffer);

        var sourceComplex = Complex(source);
        var bufferedComplex = Complex(buffered);

        //assert
        Assert.Equal(source, buffered);
        Assert.Equal(sourceComplex, bufferedComplex);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    public void ToBuffer_Calculate_Success(IEnumerable<double> source, int bufferLength)
    {
        //arrange
        var buffer = new double[bufferLength];

        //act
        var buffered = source.ToBuffer(buffer);

        source = source.Append(0);
        buffered = buffered.Append(0);

        var max = source.Max();
        var maxBuf = buffered.Max();

        var avg = source.Average();
        var avgBuf = buffered.Average();

        var sqr = source.Sum(x => x * x);
        var sqrBuf = buffered.Sum(x => x * x);

        var complex = Complex(source).Sum();
        var complexBuf = Complex(buffered).Sum();

        //assert
        Assert.Equal(avg, avgBuf);
        Assert.Equal(max, maxBuf);
        Assert.Equal(sqr, sqrBuf);
        Assert.Equal(complex, complexBuf);
    }

    static IEnumerable<double> Complex(IEnumerable<double> x) => x
        .Select(x => x - 1)
        .Where(x => x % 2 == 0)
        .GroupBy(x => x < 0)
        .Select(x => x.Sum());
}

//data
public partial class BufferiingTests
{
    public static IEnumerable<object[]> GetTestData() => GetTestDataInternal()
        .Select(x => (source: GenerateSource(x.sourceLength), x.bufferLength))
        .Select(x => new object[] { x.source, x.bufferLength });

    private static IEnumerable<double> GenerateSource(int length) => Enumerable
        .Range(0, length)
        //.Select(_ => Random.Shared.Next(-100, 100))
        .Select(x => (double)x)
        .ToArray(); //materialize random

    private static IEnumerable<(int sourceLength, int bufferLength)> GetTestDataInternal()
    {
        yield return (0, 0); //full empty
        yield return (10, 0); //empty buffer
        yield return (0, 10); //empty source
        yield return (10, 10); //equal buffer
        yield return (10, 20); //large buffer
        yield return (10, 19);
        yield return (10, 11);
        yield return (20, 19); //small buffer
        yield return (20, 15);
        yield return (20, 11);
        yield return (20, 10); //very small buffer
        yield return (20, 9);
        yield return (20, 3);
        yield return (20, 1);
    }
}
