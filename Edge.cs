namespace SytyRouting
{
    [Serializable]
    public class Edge
    {
        public long Id {get; set;}
        public double Cost {get; set;}
         
        public Node? SourceNode {get; set;}
        public Node? TargetNode {get; set;}

        public void WriteToStream(BinaryWriter bw, Dictionary<long, int> indexes)
        {
            bw.Write(Id);
            bw.Write(Cost);
            bw.Write(indexes[TargetNode.Id]);
        }

        public void ReadFromStream(BinaryReader br, Node[] array, Node source)
        {
            Id = br.ReadInt64();
            Cost = br.ReadDouble();
            TargetNode = array[br.ReadInt32()];
            TargetNode.InwardEdges.Add(this);
            SourceNode = source;
            source.OutwardEdges.Add(this);
        }
    }
}