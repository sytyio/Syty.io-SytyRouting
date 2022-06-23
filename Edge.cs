using System.Diagnostics.CodeAnalysis;

namespace SytyRouting
{
    [Serializable]
    public class Edge
    {
        public long OsmID { get; set; }
        public double Cost {get; set;}

        [NotNull] 
        public Node? SourceNode {get; set;}
        
        [NotNull] 
        public Node? TargetNode {get; set;}

        public void WriteToStream(BinaryWriter bw)
        {
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            bw.Write(OsmID);
            bw.Write(Cost);
            bw.Write(SourceNode.Idx);
            bw.Write(TargetNode.Idx);
        }

        public void ReadFromStream(byte[] bytes, ref int pos, Node[] array)
        {
            OsmID = BitHelper.ReadInt64(bytes, ref pos);
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            Cost = BitHelper.ReadDouble(bytes, ref pos);
            SourceNode = array[BitHelper.ReadInt32(bytes, ref pos)];
            SourceNode.OutwardEdges.Add(this);
            TargetNode = array[BitHelper.ReadInt32(bytes, ref pos)];
            TargetNode.InwardEdges.Add(this);
        }
    }
}