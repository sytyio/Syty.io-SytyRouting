
using System.Diagnostics.CodeAnalysis;

namespace SytyRouting
{
    public class KDNode
    {
        [NotNull]
        public Node? Item { get;  set; }
        public KDNode? Low { get; set; }
        public KDNode? High { get; set; }

        public int Orientation { get; set; }
    }
}