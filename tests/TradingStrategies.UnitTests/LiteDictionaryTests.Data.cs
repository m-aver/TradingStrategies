using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingStrategies.UnitTests
{
    public partial class LiteDictionaryTests
    {
        //must return keys with unique hash codes
        public static IEnumerable<object[]> GetSuccessData(int itemsCount)
        {
            var withoutCollisions =
                Enumerable.Range(1, itemsCount)
                .Select(x => (value: new object(), key: x))
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [withoutCollisions];

            var fullCollision =
                Enumerable.Range(1, itemsCount)
                .Select(x => (value: new object(), key: x * itemsCount))
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [fullCollision];

            var partialCollision =
                Enumerable.Range(1, itemsCount)
                .Select(x => (value: new object(), key: x * (itemsCount / 2) + 1))
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [partialCollision];
        }

        //must return keys with duplicate hash codes
        public static IEnumerable<object[]> GetInvalidData_NonUniqueHashCodes()
        {
            var itemsCount = 10;

            var oneDuplicate =
                Enumerable.Range(1, itemsCount)
                .Select(x => (value: new object(), key: x))
                .Union([(value: new object(), key: 1)])
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [oneDuplicate];

            var oneDuplicateWithCollision =
                Enumerable.Range(1, itemsCount)
                .Select(x => (value: new object(), key: x * itemsCount))
                .Union([(value: new object(), key: 1 * itemsCount)])
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [oneDuplicateWithCollision];

            var allDuplicates =
                Enumerable.Range(1, itemsCount)
                .Select(x => (value: new object(), key: 1))
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [allDuplicates];
        }

        //must return keys with negative or zero hash codes
        public static IEnumerable<object[]> GetInvalidData_NonPositiveHashCodes()
        {
            var itemsCount = 10;

            var zero =
                Enumerable.Range(-itemsCount / 2, itemsCount)
                .Select(x => (value: new object(), key: Math.Abs(x)))
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [zero];

            var negative =
                Enumerable.Range(-itemsCount / 2, itemsCount)
                .Select(x => (value: new object(), key: x * -1))
                .Select(x => ((object)x.value, (object)x.key))
                .ToArray();

            yield return [negative];
        }
    }
}
