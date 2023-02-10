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

        public Edge GetFirstOutboundEdge(byte transportMode)
        {
            Edge edge = null!;
            foreach(var outwardEdge in OutwardEdges)
            {
                if((outwardEdge.TransportModes & transportMode) != 0)
                {
                    edge = outwardEdge;
                    break;
                }
            }

            return edge;
        }

        public List<Edge> GetOutboundEdges(byte transportMode)
        {
            Edge edge = null!;
            List<Edge> edges = new List<Edge>(0);
            //debug:
            if(Idx==1562550)
            {
                Console.WriteLine("Probe 1562550");
            }
            //
            foreach(var outwardEdge in OutwardEdges)
            {
                if((outwardEdge.TransportModes & transportMode) == transportMode)
                {
                    edge = outwardEdge;
                    edges.Add(edge);
                }
            }

            //debug:
            if(edge == null)
            {
                Console.WriteLine("No edge found with the '{0}' transport mode at node {1}.", TransportModes.SingleMaskToString(transportMode), Idx);
            }
            //

            return edges;
        }

        public Edge GetFirstInboundEdge(byte transportMode)
        {
            Edge edge = null!;
            foreach(var inwardEdge in InwardEdges)
            {
                if((inwardEdge.TransportModes & transportMode) != 0)
                {
                    edge = inwardEdge;
                    break;
                }
            }

            return edge;
        }

        public int[] GetOutboundEdgeTypes()
        {            
            List<int> edgeTypes = new List<int>(0);
            foreach(var outwardEdge in OutwardEdges)
            {
                edgeTypes.Add(outwardEdge.TagIdRouteType);
            }

            return edgeTypes.ToArray();
        }

        public int[] GetInboundEdgeTypes()
        {            
            List<int> edgeTypes = new List<int>(0);
            foreach(var inwardEdge in InwardEdges)
            {
                edgeTypes.Add(inwardEdge.TagIdRouteType);
            }

            return edgeTypes.ToArray();
        }
    }
}