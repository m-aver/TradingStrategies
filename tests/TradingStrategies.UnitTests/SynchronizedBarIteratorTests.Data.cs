using System;
using WealthLab;

namespace TradingStrategies.UnitTests
{
    public partial class SynchronizedBarIteratorTests
    {
        public static IEnumerable<object[]> GetBarDates(int seriesCount)
        {
            var barsCount = 100;

            var linear = Enumerable
                .Range(0, seriesCount)
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(x) + TimeSpan.FromSeconds(y))
                    .ToArray())
                .ToArray();

            yield return [linear];

            var parallel = Enumerable
                .Range(0, seriesCount)
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(y))
                    .ToArray())
                .ToArray();

            yield return [parallel];

            var overlaped = Enumerable
                .Range(0, seriesCount)
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(x) + TimeSpan.FromHours(y))
                    .ToArray())
                .ToArray();

            yield return [overlaped];

            var reverseOverlaped = Enumerable
                .Range(0, seriesCount)
                .OrderDescending()
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(x) + TimeSpan.FromHours(y))
                    .OrderDescending()
                    .ToArray())
                .ToArray();

            yield return [reverseOverlaped];
        }

        public static IEnumerable<object[]> GetRandomBarDates(int testsCount)
        {
            for (int i = 0; i < testsCount; i++)
            {
                var seriesCount = Random.Shared.Next(1, 10);
                var barsCount = () => Random.Shared.Next(10, 100);
                var hoursOffset = () => Random.Shared.Next(1, 1000);

                var random = Enumerable
                    .Range(0, seriesCount)
                    .Select(x => Enumerable
                        .Range(0, barsCount())
                        .Select(y => DateTime.Now + TimeSpan.FromHours(hoursOffset()))
                        .ToArray())
                    .ToArray();

                yield return [random];
            }
        }

        public static IEnumerable<object[]> GetEmptyBarDates()
        {
            var noEmptySeriesCount = 10;
            var noEmptyBarsCount = 100;
            var emptySeriesCount = 1;

            var firstEmpty =
                GetEmptySeries(emptySeriesCount)
                .Union(
                    GetSeries(noEmptySeriesCount, noEmptyBarsCount)
                );

            yield return [firstEmpty];

            var lastEmpty =
                GetSeries(noEmptySeriesCount, noEmptyBarsCount)
                .Union(
                    GetEmptySeries(emptySeriesCount)
                );

            yield return [lastEmpty];

            var amidEmpty =
                GetSeries(noEmptySeriesCount / 2, noEmptyBarsCount)
                .Union(
                    GetEmptySeries(emptySeriesCount)
                )
                .Union(
                    GetSeries(noEmptySeriesCount / 2, noEmptyBarsCount)
                );

            yield return [amidEmpty];

            var onlyEmpty =
                GetEmptySeries(emptySeriesCount);

            yield return [onlyEmpty];

            static DateTime[][] GetEmptySeries(int seriesCount)
            {
                return GetSeries(seriesCount, 0);
            }

            static DateTime[][] GetSeries(int seriesCount, int barsCount)
            {
                return Enumerable
                    .Range(0, seriesCount)
                    .Select(x => Enumerable
                        .Range(0, barsCount)
                        .Select(y => DateTime.Now + TimeSpan.FromDays(y))
                        .ToArray())
                    .ToArray();
            }
        }
    }
}