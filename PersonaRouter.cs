using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Persona[] PersonasArray = new Persona[0];

        public async Task DBPersonaLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.ConnectionString;

            var tableName = "public.persona";

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var totalDbRows = await Helper.DbTableRowsCount(connection, tableName, logger);

            // Dictionary<int, Persona> personas = new Dictionary<int, Persona>();          // Total elapsed time: 00:02:37.178
            Dictionary<int, Persona> personas = new Dictionary<int, Persona>(totalDbRows);  // Total elapsed time: 00:02:45.795


            // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
            //                     0              1              2
            var queryString = "SELECT id, home_location, work_location FROM " + tableName;
            
            connection.TypeMapper.UseNetTopologySuite();

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                long dbRowsProcessed = 0;

                while(await reader.ReadAsync())
                {
                    var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                    var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                    var workLocation = (Point)reader.GetValue(2); // work_location (Point)

                    CreatePersona(id, homeLocation, workLocation, personas);

                    dbRowsProcessed++;

                    if (dbRowsProcessed % 50_000 == 0)
                    {                        
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        Helper.SetCreationBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds, logger);
                    }
                }

                PersonasArray = personas.Values.ToArray();

                stopWatch.Stop();
                var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
                logger.Info("Persona set creation time          (HH:MM:S.mS) :: " + totalTime);
                logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
            }
        }

        public void TracePersonas()
        {
            foreach (var persona in PersonasArray)
            {
                logger.Trace("Persona: Id = {0},\n\t\t HomeLocation = ({1}, {2}),\n\t\t WorkLocation = ({3}, {4})", 
                    persona.Id, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                    persona.WorkLocation?.X, persona.WorkLocation?.Y);
            }
        }

        private Persona CreatePersona(int id, Point homeLocation, Point workLocation, Dictionary<int, Persona> personas)
        {           
            var persona = new Persona { Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
            personas.Add(id, persona);

            return personas[id];
        }
    }
}