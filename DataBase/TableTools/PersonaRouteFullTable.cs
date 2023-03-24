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
            
            int numberOfRows = 1360;//60; //1360;
            //int numberOfRows = await Helper.DbTableRowCount(originalPersonaTable, logger);

            string baseRouteTable = Configuration.PersonaRouteTable;
            var newRouteTable = baseRouteTable + "_full";

            var personaRouteTable = new DataBase.PersonaRouteTable(connectionString);
            await personaRouteTable.CreateDataSet(originalPersonaTable,newRouteTable,numberOfRows);
        }
    }
}