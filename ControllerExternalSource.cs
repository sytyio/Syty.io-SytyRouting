namespace SytyRouting.Model
{
    /** This class allows to retrieve a collection of nodes and edges without regard to the type of the source

        Gtfs use .txt with csv
        Database use a database 

        GetNodes gives a list of nodes. This list of nodes is to be connected with the graph. (KDTree)
        GetInternalNodes gives a list of nodes that should not be connected with the graph. 

        GetEdges returns the list of edges created between the different nodes.

        GetEdges and GetInternalNodes can be null.

        InitController will fill the node/edge collections.

        Clean() offers a cleaning of useless files and data.

        You must first call InitController before you can get the nodes and eventually the edges. 



    */
    interface ControllerExternalSource
    {
        Task Initialize();
        IEnumerable<Node> GetNodes();
        IEnumerable<Edge> GetEdges();

        IEnumerable<Node> GetInternalNodes();

        void Clean();
    }
}