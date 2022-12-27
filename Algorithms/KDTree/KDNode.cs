
using System.Diagnostics.CodeAnalysis;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.KDTree
{
    public class KDNode
    {
        [NotNull]
        public Node? Item = null!;
        public KDNode? Low;
        public KDNode? High;

        public int Orientation;
    }
}