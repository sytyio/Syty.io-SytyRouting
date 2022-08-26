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
            if (edgeOsmID == 0)
            {
                throw new Exception("Invalid data imported");
            }
            bw.Write(edgeOsmID);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(M);
        }
    }
}