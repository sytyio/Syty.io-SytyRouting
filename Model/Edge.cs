using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.Geometries;

namespace SytyRouting.Model
{
    public struct XYMPoint
    {
        public double X;
        public double Y;
        public double M;
    }

    public class Edge
    {
        public long OsmID;
        public double Cost;
        public OneWayState OneWayState;
        public double LengthM;
        public double MaxSpeedMPerS;
        public byte TransportModes;

        [NotNull]
        public Node? SourceNode = null!;

        [NotNull]
        public Node? TargetNode = null!;

        public XYMPoint[]? InternalGeometry;

        public void WriteToStream(BinaryWriter bw)
        {
            if (OsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            bw.Write(OsmID);
            bw.Write(Cost);
            bw.Write(LengthM);
            bw.Write(MaxSpeedMPerS);
            bw.Write(TransportModes);
            bw.Write(SourceNode.Idx);
            bw.Write(TargetNode.Idx);

            if(InternalGeometry != null)
            {
                bw.Write((ushort)InternalGeometry.Length);
                foreach(var xymPoint in InternalGeometry)
                {
                    bw.Write(xymPoint.X);
                    bw.Write(xymPoint.Y);
                    bw.Write(xymPoint.M);
                }
            }
            else
            {
                bw.Write((ushort)0);
            }
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
            MaxSpeedMPerS = br.ReadDouble();
            TransportModes = br.ReadByte();
            SourceNode = array[br.ReadInt32()];
            TargetNode = array[br.ReadInt32()];
            
            var internalGeometryLength = br.ReadUInt16();
            if(internalGeometryLength > 0)
            {
                InternalGeometry = new XYMPoint[internalGeometryLength];
                for(int i = 0; i < InternalGeometry.Length; i++)
                {
                    InternalGeometry[i].X = br.ReadDouble();
                    InternalGeometry[i].Y = br.ReadDouble();
                    InternalGeometry[i].M = br.ReadDouble();
                }
            }
        }

        public void SetCost(CostCriteria costCriteria)
        {                               
            switch(costCriteria)
            {
                case CostCriteria.MinimalTravelTime:
                {
                    Cost =  LengthM / MaxSpeedMPerS;
                    break;
                }                
                case CostCriteria.MinimalTravelDistance:
                {
                    Cost = LengthM;
                    break;
                }
            }

            if(OneWayState == OneWayState.Reversed)
            {
                Cost = -1*Cost;                
            }
        }
    }
}