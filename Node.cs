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
        public List<Edge> InwardEdges {get; set;} = new List<Edge>();
        public List<Edge> OutwardEdges {get; set;} = new List<Edge>();

        public void WriteToStream(BinaryWriter bw)
        {
            bw.Write(OsmID);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(ValidTarget);
            bw.Write(ValidSource);
            bw.Write(OutwardEdges.Count);
            foreach(var edge in OutwardEdges)
            {
                edge.WriteToStream(bw);
            }
        }

        public void ReadFromStream(BinaryReader br, Node[] array)
        {
            OsmID = br.ReadInt64();
            X = br.ReadDouble();
            Y = br.ReadDouble();
            ValidTarget = br.ReadBoolean();
            ValidSource = br.ReadBoolean();
            var count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var edge = new Edge();
                edge.ReadFromStream(br, array, this);
            }
        }    
    }
}