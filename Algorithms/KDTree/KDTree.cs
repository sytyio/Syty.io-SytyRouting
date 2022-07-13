using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.KDTree
{
    public class KDTree
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private KDNode? root;

        public KDTree(IEnumerable<Node> dataset)
        {
            logger.Debug("Creating a new KD-Tree");
            root = SplitSet(dataset, 0);
            var count = CountItems(root);
            if(count != dataset.Count())
            {
                throw new Exception("Invalid data!");
            }
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
            var middle = sorted.ElementAt(middleIdx);
            var subLow = sorted.Take(middleIdx).ToList();
            var subHigh = sorted.Skip(middleIdx + 1).ToList();
            
            var result =  new KDNode
            {
                Item = middle,
                Orientation = orientation,
                Low = SplitSet(subLow, (orientation + 1) % 2),
                High = SplitSet(subHigh, (orientation + 1) % 2)
            };

            return result;
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
                Low = br.ReadBoolean() ? ReadFromStream(br, array, (orientation + 1) % 2) : null,
                High = br.ReadBoolean() ? ReadFromStream(br, array, (orientation + 1) % 2) : null
            };
        }

        public Node GetNearestNeighbor(double x, double y, bool isTarget = true, bool isSource = true)
        {
            logger.Trace("Query for x: {0} y: {1}", x, y);
            if (root != null)
            {
                var result = GetNearestNeighbor(x, y, root, double.MaxValue, isTarget, isSource).Item1;
                if (result != null)
                {
                    return result;
                }
            }
            throw new Exception("Impossible to query.");
        }

        private (Node?, double) GetNearestNeighbor(double x, double y, KDNode? currentNode, double maxDist, bool isTarget, bool isSource)
        {
            if(currentNode == null)
            {
                return (null, Double.MaxValue);
            }
            var lower = IsLower(x, y, currentNode);
            if (lower)
            {
                logger.Trace("  We are lower than x: {0} y: {1} on the {2}-axis", currentNode.Item.X, currentNode.Item.Y, currentNode.Orientation == 0 ? "X": "Y");
            }
            else
            {
                logger.Trace("  We are higher than x: {0} y: {1} on the {2}-axis", currentNode.Item.X, currentNode.Item.Y, currentNode.Orientation == 0 ? "X" : "Y");
            }

            var distanceCurrent = Helper.GetSquaredDistance(currentNode.Item, x, y);
            if(distanceCurrent < maxDist && (currentNode.Item.ValidSource || !isSource) && (currentNode.Item.ValidTarget || !isTarget))
            {
                maxDist = distanceCurrent;
            }
            var candidateBest = GetNearestNeighbor(x, y, lower ? currentNode.Low : currentNode.High, maxDist, isTarget, isSource);
            if(distanceCurrent < candidateBest.Item2  && (currentNode.Item.ValidSource || !isSource) && (currentNode.Item.ValidTarget || !isTarget))
            {
                candidateBest = (currentNode.Item, distanceCurrent);
            }

            if(candidateBest.Item2 < maxDist)
            {
                maxDist = candidateBest.Item2;
            }
            var bestOtherSub = GetSquaredDistanceToHalfPlane(currentNode, x, y);
            if(bestOtherSub < maxDist)
            {
                var candidateOtherSub = GetNearestNeighbor(x, y, lower ? currentNode.High : currentNode.Low, maxDist, isTarget, isSource);
                if(candidateOtherSub.Item2 < candidateBest.Item2)
                {
                    candidateBest = candidateOtherSub;
                }
            }
            if(candidateBest.Item1 != null)
              logger.Trace("  Current best is x: {0} y: {1} at distance {2}", candidateBest.Item1.X, candidateBest.Item1.Y, candidateBest.Item2);
            else
                logger.Trace("  No best found");
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

        

        private double GetSquaredDistanceToHalfPlane(KDNode n, double x, double y)
        {
            switch(n.Orientation)
            {
                case 0:
                    return (n.Item.X - x) * (n.Item.X - x);
                default:
                    return (n.Item.Y - y) * (n.Item.Y - y);
            }
        }

        private int CountItems(KDNode? n)
        {
            if(n == null)
                return 0;
            return 1 + CountItems(n.Low) + CountItems(n.High);
        }
    }
}