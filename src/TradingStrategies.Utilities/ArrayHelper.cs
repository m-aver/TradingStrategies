using System;

namespace TradingStrategies.Utilities
{
    internal static class ArrayHelper
    {
        public static void ShiftItem<T>(T[] array, int sourceIndex, int destinationIndex)
        {
            var length = destinationIndex - sourceIndex;

            if (length == 0)
            {
                return;
            }

            var shifting = array[sourceIndex];

            if (length > 0)
            {
                Array.Copy(array, sourceIndex + 1, array, sourceIndex, length);
            }
            else
            {
                Array.Copy(array, destinationIndex, array, destinationIndex + 1, -length);
            }

            array[destinationIndex] = shifting;
        }
    }
}
