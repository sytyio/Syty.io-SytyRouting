using System.Diagnostics.CodeAnalysis;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.MultiDijkstra
{

    public class DijkstraInstance
    {
        public int Idx;

        public int[] Origins = null!;
        public double[] TotalCosts = null!;

        public int[] Depths = null!;
    }

}