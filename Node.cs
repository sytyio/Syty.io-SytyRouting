namespace SytyRouting
{

    [Serializable]
    public class Node
    {
        public long Id {get; set;}
        public double X {get; set;}
        public double Y {get; set;}
        public List<Edge> InwardEdges {get; set;} = new List<Edge>();
        public List<Edge> OutwardEdges {get; set;} = new List<Edge>();

        public void WriteToStream(BinaryWriter bw, Dictionary<long,int> indexes)
        {
            bw.Write(Id);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(OutwardEdges.Count);
            foreach(var edge in OutwardEdges)
            {
                edge.WriteToStream(bw, indexes);
            }
        }

        public void ReadFromStream(BinaryReader br, Node[] array)
        {
            Id = br.ReadInt64();
            X = br.ReadDouble();
            Y = br.ReadDouble();
            var count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var edge = new Edge();
                edge.ReadFromStream(br, array, this);
            }
        }
        
    }
}