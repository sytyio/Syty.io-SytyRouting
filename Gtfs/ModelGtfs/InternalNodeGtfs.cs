using SytyRouting.Model;
namespace SytyRouting.Gtfs.ModelGtfs
{
    public class InternalNodeGtfs : Node
    {

        public string IdOriginalNode { get; set; }

        public int[] NbDepartures = new int [24];

        public override string ToString(){
            return "Parent : "+IdOriginalNode;
        }
    }
    }