using NLog;

namespace SytyRouting
{
    public class KDTree
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private KDNode? root;

        public KDTree(IEnumerable<Node> dataset)
        {
            logger.Debug("Creating a new KD-Tree");
            root = SplitSet(dataset, 0);
            logger.Debug("KD-Tree ready!");
        }

        public KDTree(BinaryReader br, Node[] array)
        {
            logger.Debug("Loading KD-Tree");
            root = ReadFromStream(br, array, 0);
            logger.Debug("KD-Tree ready!");
        }

        private KDNode? SplitSet(IEnumerable<Node> dataset, int orientation)
        {
            var count = dataset.Count();
            if (count == 0)
            {
                return null;
            }
            var sorted = orientation == 0 ? dataset.OrderBy(t => t.X).ToList() : dataset.OrderBy(t => t.Y).ToList() ;
            var middleIdx = count / 2;
            var subLow = sorted.Take(middleIdx);
            var subHigh = sorted.Skip(middleIdx + 1);
            return new KDNode
            {
                Item = dataset.ElementAt(middleIdx),
                Orientation = orientation,
                Low = SplitSet(subLow, orientation + 1 % 2),
                High = SplitSet(subHigh, orientation + 1 % 2)
            };
        }

        public void WriteToStream(BinaryWriter bw)
        {
            if (root != null)
            {
                WriteToStream(bw, root);
            }
        }

        private void WriteToStream(BinaryWriter bw, KDNode n)
        {
            bw.Write(n.Item.Idx);
            if(n.Low != null)
            {
                bw.Write(true);
                WriteToStream(bw, n.Low);
            }
            else
            {
                bw.Write(false);
            }
            if(n.High != null)
            {
                bw.Write(true);
                WriteToStream(bw, n.High);
            }
            else
            {
                bw.Write(false);
            }
        }

        private KDNode ReadFromStream(BinaryReader br, Node[] array, int orientation)
        {
            var idx = br.ReadInt32();
            return new KDNode
            {
                Item = array[idx],
                Orientation = orientation,
                Low = br.ReadBoolean() ? ReadFromStream(br, array, orientation + 1 % 2) : null,
                High = br.ReadBoolean() ? ReadFromStream(br, array, orientation + 1 % 2) : null
            };
        }

        public Node GetNearestNeighbor(double x, double y)
        {
            if (root != null)
            {
                var result = GetNearestNeighbor(x, y, root).Item1;
                if (result != null)
                {
                    return result;
                }
            }
            throw new Exception("Impossible to query.");
        }

        private (Node?, double) GetNearestNeighbor(double x, double y, KDNode? currentNode)
        {
            if(currentNode == null)
            {
                return (null, Double.MaxValue);
            }

            var candidateBest = GetNearestNeighbor(x, y, IsLower(x,y, currentNode) ? currentNode.Low : currentNode.High);
            var distanceCurrent = GetDistance(currentNode.Item, x, y);
            if(distanceCurrent < candidateBest.Item2)
            {
                candidateBest = (currentNode.Item, distanceCurrent);
            }
            var bestOtherSub = GetDistance(currentNode, x, y);
            if(bestOtherSub < candidateBest.Item2)
            {
                var candidateOtherSub = GetNearestNeighbor(x, y, IsLower(x,y, currentNode) ? currentNode.High : currentNode.Low);
                if(candidateOtherSub.Item2 < candidateBest.Item2)
                {
                    candidateBest = candidateOtherSub;
                }
            }
            return candidateBest;
        }

        private bool IsLower(double x, double y, KDNode n)
        {
            switch(n.Orientation)
            {
                case 0:
                    return x < n.Item?.X;
                default:
                    return y < n.Item?.Y;
            }
        }

        private double GetDistance(double x1, double y1, double x2, double y2)
        {
            return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
        }

        private double GetDistance(Node n, double x, double y)
        {
            return GetDistance(n.X, n.Y, x, y);
        }

        private double GetDistance(KDNode n, double x, double y)
        {
            switch(n.Orientation)
            {
                case 0:
                    return (n.Item.X - x) * (n.Item.X - x);
                default:
                    return (n.Item.Y - y) * (n.Item.Y - y);
            }
        }
    }
}