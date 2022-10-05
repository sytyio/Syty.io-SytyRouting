namespace SytyRouting.Gtfs.ModelGtfs
{
    public class AgencyGtfs
    {

        public string Id { get; set; }

        public string Name {get;set;}

        public string Url {get;set;}

        public override string ToString()
        {
            return "Id = " + Id + " Name = " + Name + " Url = "+Url;
        }

        public AgencyGtfs(string id, string name, string url){
            Id=id;
            Name=name;
            Url=url;
        }
    }
}