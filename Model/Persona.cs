using NetTopologySuite.Geometries;

namespace SytyRouting.Model
{

    public class Persona
    {
        public int Idx;
        public int Id;
        public Point? HomeLocation;
        public Point? WorkLocation;
    }
}