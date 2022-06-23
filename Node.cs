using System.Diagnostics.CodeAnalysis;

namespace SytyRouting
{

    public class Node
    {
        public int Idx {get; set;}

        public long OsmID { get; set; }
        public double X {get; set;}
        public double Y {get; set;}

        public bool ValidTarget { get; set; }

        public bool ValidSource { get; set; }

        [NotNull] 
        public List<Edge>? InwardEdges {get; set;}

        [NotNull] 
        public List<Edge>? OutwardEdges {get; set;}

        public void WriteToStream(BinaryWriter bw)
        {
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            bw.Write(OsmID);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(ValidTarget);
            bw.Write(ValidSource);
            bw.Write(InwardEdges.Count);
            bw.Write(OutwardEdges.Count);
        }

        public void ReadFromStream(byte[] bytes, ref int pos)
        {
            OsmID = BitHelper.ReadInt64(bytes, ref pos);
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            X = BitHelper.ReadDouble(bytes, ref pos);
            Y = BitHelper.ReadDouble(bytes, ref pos);
            ValidTarget = BitHelper.ReadBoolean(bytes, ref pos);
            ValidSource = BitHelper.ReadBoolean(bytes, ref pos);
            InwardEdges = new List<Edge>(BitHelper.ReadInt32(bytes, ref pos));
            OutwardEdges = new List<Edge>(BitHelper.ReadInt32(bytes, ref pos));
        }
        
    }
}