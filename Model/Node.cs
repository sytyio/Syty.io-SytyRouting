namespace SytyRouting.Model
{

    public class Node
    {
        public int Idx;

        public long OsmID;
        public double X;
        public double Y;

        public bool ValidTarget;
        public bool ValidSource;

        public List<Edge> InwardEdges = new List<Edge>(4);
        public List<Edge> OutwardEdges = new List<Edge>(4);

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

        public bool IsAValidRouteStart(byte[] requestedTransportModes)
        {
            byte requestedTransportModesMask = TransportModes.ArrayToMask(requestedTransportModes);
            foreach(var outwardEdge in OutwardEdges)
            {
                if((outwardEdge.TransportModes & requestedTransportModesMask) != 0)
                    return true; 
            }
            return false;
        }

        public bool IsAValidRouteEnd(byte[] requestedTransportModes)
        {
            byte requestedTransportModesMask = TransportModes.ArrayToMask(requestedTransportModes);
            foreach(var inwardEdge in InwardEdges)
            {
                if((inwardEdge.TransportModes & requestedTransportModesMask) != 0)
                    return true; 
            }
            return false;
        }

        public byte GetAvailableOutboundTransportModes()
        {
            byte transportModes = 0;
            foreach(var outwardEdge in OutwardEdges)
            {
                transportModes |= outwardEdge.TransportModes; 
            }
            return transportModes;
        }

        public byte GetAvailableInboundTransportModes()
        {
            byte transportModes = 0;
            foreach(var inwardEdge in InwardEdges)
            {
                transportModes |= inwardEdge.TransportModes; 
            }
            return transportModes;
        }
    }
}