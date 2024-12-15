using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//TODO: макс число пуллов, макс число буфферов нод (политика неразрастания)

namespace TradingStrategies.Utilities
{
    //factory
    internal partial class NodePool
    {
        private static readonly ConcurrentDictionary<int, NodePool> _pools;

        static NodePool()
        {
            _pools = new ConcurrentDictionary<int, NodePool>();
        }

        private NodePool()
        {
        }

        public static NodePool CreateOrGet(int nodesCount)
        {
            return _pools.GetOrAdd(nodesCount, (cnt) =>
            {
                return new NodePool(Environment.ProcessorCount, cnt);
            });
        }
    }

    //pool
    internal partial class NodePool
    {
        private readonly ConcurrentStack<Node[]> _nodes;
        private readonly int _count;
        private readonly int _concurrency;

        private NodePool(int concurrency, int count)
        {
            _concurrency = concurrency;
            _count = count;
            _nodes = new ConcurrentStack<Node[]>(
                Enumerable.Range(0, concurrency).Select(x => NewNodes(count)));
        }

        internal Node[] Rent()
        {
            var nodes = _nodes.TryPop(out var rent) ? rent : NewNodes(_count);
            return nodes;
        }

        internal void Return(Node[] nodes)
        {
            _nodes.Push(nodes);
        }

        private static Node[] NewNodes(int count)
        {
            var nodes = new Node[count];

            for (int i = 0; i < count; i++)
            {
                nodes[i] = new Node();
            }

            return nodes;
        }
    }

    //adapter to array pool
    internal partial class NodePool : ArrayPool<Node>
    {
        public override Node[] Rent(int minimumLength)
        {
            if (minimumLength > _count)
            {
                throw new ArgumentException(
                    $"this {nameof(NodePool)} instance is not configured for passed length " +
                    $"(passed:{minimumLength}; configured:{_count})", 
                    nameof(minimumLength));
            }

            return Rent();
        }

        public override void Return(Node[] array, bool clearArray = false)
        {
            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }

            Return(array);
        }
    }

    //for testing
    internal class FakeNodePool : ArrayPool<Node>
    {
        public override Node[] Rent(int minimumLength)
        {
            return new Node[minimumLength];
        }

        public override void Return(Node[] array, bool clearArray = false)
        {
        }
    }
}
