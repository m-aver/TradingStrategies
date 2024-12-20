using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingStrategies.Utilities;

namespace TradingStrategies.UnitTests
{
    public partial class LiteDictionaryTests
    {
        [Theory]
        [MemberData(nameof(GetSuccessData), parameters: 1)]
        [MemberData(nameof(GetSuccessData), parameters: 10)]
        [MemberData(nameof(GetSuccessData), parameters: 100)]
        public void Success(IEnumerable<(object value, object key)> data)
        {
            //arrange
            var values = data.Select(x => x.value).ToArray();
            var keys = data.Select(x => x.key).ToArray();

            var map = new LiteDictionary<object, object>(keys, values);

            //act
            var resultValues = keys.Select(x => map[x]).ToArray();

            //assert
            Assert.Equal(values, resultValues);
        }

        [Theory]
        [MemberData(nameof(GetInvalidData_NonUniqueHashCodes))]
        [MemberData(nameof(GetInvalidData_NonPositiveHashCodes))]
        public void Create_InvalidHashCodes_Exception(IEnumerable<(object value, object key)> data)
        {
            //arrange
            var values = data.Select(x => x.value).ToArray();
            var keys = data.Select(x => x.key).ToArray();

            //act
            //assert
            Assert.Throws<ArgumentException>(() 
                => new LiteDictionary<object, object>(keys, values));
        }
    }
}
