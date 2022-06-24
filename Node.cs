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
        public List<Edge> InwardEdges { get; set; } = new List<Edge>(4);
        public List<Edge> OutwardEdges {get; set;} = new List<Edge>(4);

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
        }

        public void ReadFromStream(BinaryReader br)
        {
            OsmID = br.ReadInt64();
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            X = br.ReadDouble();
            Y = br.ReadDouble();
            ValidTarget = br.ReadBoolean();
            ValidSource = br.ReadBoolean();
        }
    }
}