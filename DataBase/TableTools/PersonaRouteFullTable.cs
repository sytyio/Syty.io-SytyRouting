using NLog;

namespace SytyRouting.DataBase
{
    public class PersonaRouteFullTable
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task Create()
        {
            string connectionString = Configuration.ConnectionString;

            var originalPersonaTable = Configuration.PersonaTable;
            
            //int numberOfRows = 1360; // For the sake of testing. (In the name of Science and in all its glory.)
            int numberOfRows = await Helper.DbTableRowCount(originalPersonaTable, logger);

            string baseRouteTable = Configuration.PersonaRouteTable;
            var newRouteTable = baseRouteTable + "_full";

            var personaRouteTable = new DataBase.PersonaRouteTable(connectionString);
            await personaRouteTable.CreateDataSet(originalPersonaTable,newRouteTable,numberOfRows);
        }
    }
}