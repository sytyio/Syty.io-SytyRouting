using System.Diagnostics.CodeAnalysis;

namespace SytyRouting.Model
{
    public class Edge
    {
        public long OsmID { get; set; }
        public double Cost {get; set;}
        public double LengthM { get; set; }

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
            bw.Write(LengthM);
            bw.Write(SourceNode.Idx);
            bw.Write(TargetNode.Idx);
        }

        public void ReadFromStream(BinaryReader br, Node[] array)
        {
            OsmID = br.ReadInt64();
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            Cost = br.ReadDouble();
            LengthM = br.ReadDouble();
            SourceNode = array[br.ReadInt32()];
            TargetNode = array[br.ReadInt32()];
            
        }
    }
}