namespace SytyRouting
{
    [Serializable]
    public class Edge
    {
        public long Id {get; set;}
        public double Cost {get; set;}
        public Node? EndNode {get; set;}


        public void WriteToStream(BinaryWriter bw, Dictionary<long, int> indexes)
        {
            bw.Write(Id);
            bw.Write(Cost);
            bw.Write(indexes[EndNode.Id]);
        }

        public void ReadFromStream(BinaryReader br, Node[] array)
        {
            Id = br.ReadInt64();
            Cost = br.ReadDouble();
            EndNode = array[br.ReadInt32()];
        }
    }
}