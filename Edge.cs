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
            if(TargetNode == null)
            {
                throw new Exception("Incorrectly initialized structure");
            }
            bw.Write(TargetNode.Idx);
        }

        public void ReadFromStream(BinaryReader br, Node[] array, Node source)
        {
            OsmID = br.ReadInt64();
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            Cost = br.ReadDouble();
            TargetNode = array[br.ReadInt32()];
            TargetNode.InwardEdges.Add(this);
            SourceNode = source;
            source.OutwardEdges.Add(this);
        }
    }
}