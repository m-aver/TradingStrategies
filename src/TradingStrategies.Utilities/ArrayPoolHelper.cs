using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

//позволяет хранить сконфигурированные пулы - у стандартного ArrayPool.Shared слишком низкое значение maxArraysPerBucket
//это приводит к тому, что пул подтекает - при переполнении бакетов просто создаются новые массивы без пуллинга

//poolsCount нужно чтобы не допустить неконтроллируемого разрастания пуллов и памяти
//для работы с WealthLab - poolsCount определяет максимальное количество параллельных сессии оптимизации
//превышение приведет к вытеснению пулов из общедоступных и по факту пуллинга не будет
//необходимо чтобы (maxArrayLength, maxArraysPerBucket) были одинаковыми в рамках одной сессии оптимизации
//этого легко обеспечить из условия что в SynchronizedBarIterator всегда передается одна и таже коллекция Bars в рамках одной сессии
//например для ticks: ArrayPoolHelper<long>.CreateOrGet(barCollection.Max(x => x.Count), barsCount * Environment.ProcessorCount)

namespace TradingStrategies.Utilities
{
    /// <summary>
    /// Allows to configure shared array pool instance
    /// </summary>
    internal static partial class ArrayPoolHelper<T>
    {
        private const int poolsCount = 10;   //max count of simultaneously existed pools
        private static readonly Dictionary<(int, int), ArrayPool<T>> _sharedPools = new(poolsCount);
        private static SpinLock _spinLock = new(Debugger.IsAttached);

        public static ArrayPool<T> CreateOrGet(int maxArrayLength, int maxArraysPerBucket)
        {
            var lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                if (_sharedPools.TryGetValue((maxArrayLength, maxArraysPerBucket), out var pool))
                {
                    return pool;
                }
                else
                {
                    if (_sharedPools.Count >= poolsCount)
                    {
                        _sharedPools.Remove(_sharedPools.First().Key);
                    }
                    var newPool = Create(maxArrayLength, maxArraysPerBucket);
                    _sharedPools.Add((maxArrayLength, maxArraysPerBucket), newPool);
                    return newPool;
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _spinLock.Exit();
                }
            }
        }

        private static ArrayPool<T> Create(int maxArrayLength, int maxArraysPerBucket)
        {
            return ArrayPool<T>.Create(maxArrayLength, maxArraysPerBucket);
        }
    }

    //проще, надежнее, быстрее. но руками
    internal static partial class ArrayPoolHelper<T>
    {
        public const int MaxArrayLength = 10000; //максимальное число свечей в датасетах
        public const int MaxArraysPerBucket = 50; //максимальное число датасетов
        public static readonly int MaxArraysPerBucketConcurrent = MaxArraysPerBucket * Environment.ProcessorCount;

        public static readonly ArrayPool<T> PreconfiguredShared = ArrayPool<T>.Create(MaxArrayLength, MaxArraysPerBucketConcurrent);
    }
}
