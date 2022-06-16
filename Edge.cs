namespace SytyRouting
{
    [Serializable]
    public class Edge
    {
        public long OsmID { get; set; }
        public double Cost {get; set;}
         
        public Node? SourceNode {get; set;}
        public Node? TargetNode {get; set;}

        public void WriteToStream(BinaryWriter bw)
        {
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
            Cost = br.ReadDouble();
            TargetNode = array[br.ReadInt32()];
            TargetNode.InwardEdges.Add(this);
            SourceNode = source;
            source.OutwardEdges.Add(this);
        }
    }
}