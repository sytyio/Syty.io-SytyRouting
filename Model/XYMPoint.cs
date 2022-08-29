using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.Geometries;

namespace SytyRouting.Model
{
    public class XYMPoint
    {
        public long edgeOsmID; // it is not unique! maybe change to 'gid' [PK on database]
        public double X; // Longitude
        public double Y; // Latitude

        public double M; // M-coordinate (time stamp? percege or relative portion of the entire line?)

        public void WriteToStream(BinaryWriter bw)
        {
            bw.Write(edgeOsmID);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(M);
        }

        public void ReadFromStream(BinaryReader br)
        {
            edgeOsmID = br.ReadInt64();
            X = br.ReadDouble();
            Y = br.ReadDouble();
            M = br.ReadDouble();
        }
    }
}