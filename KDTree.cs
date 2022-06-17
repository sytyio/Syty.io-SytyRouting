
using NLog;

namespace SytyRouting
{

    public class KDNode
    {
        public Node? Item { get;  set; }
        public KDNode? Low { get; set; }
        public KDNode? High { get; set; }
    }

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
            root = ReadFromStream(br, array);
            logger.Debug("KD-Tree ready!");
        }

        private KDNode? SplitSet(IEnumerable<Node> dataset, int direction)
        {
            var count = dataset.Count();
            if (count == 0)
            {
                return null;
            }
            var sorted = direction == 0 ? dataset.OrderBy(t => t.X).ToList() : dataset.OrderBy(t => t.Y).ToList() ;
            var middleIdx = count / 2;
            var subLow = sorted.Take(middleIdx);
            var subHigh = sorted.Skip(middleIdx + 1);
            return new KDNode
            {
                Item = dataset.ElementAt(middleIdx),
                Low = SplitSet(subLow, direction + 1 % 2),
                High = SplitSet(subHigh, direction + 1 * 2)
            };
        }

        public void WriteToStream(BinaryWriter bw)
        {
            WriteToStream(bw, root);
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

        private KDNode ReadFromStream(BinaryReader br, Node[] array)
        {
            var idx = br.ReadInt32();
            return new KDNode
            {
                Item = array[idx],
                Low = br.ReadBoolean() ? ReadFromStream(br, array) : null,
                High = br.ReadBoolean() ? ReadFromStream(br, array) : null
            };
        }
    }
}