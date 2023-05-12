using NetTopologySuite.Geometries;
using NLog;
using Npgsql;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public abstract class BaseRouteUploader : IRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public async Task SetAuxiliaryTable(NpgsqlConnection connection, string auxiliaryTable, string auxiliaryTablePK)
        {
            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();

            await using var auxTableBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("CREATE TEMPORARY TABLE IF NOT EXISTS " + auxiliaryTable + " (persona_id INT);"),
                    new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTablePK + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTablePK + " PRIMARY KEY (persona_id);"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route GEOMETRY;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route_2d GEOMETRY;"), // uncomment this line to get a quick view of the routing trajectory on TablePlus. (Modify also resultsBatch and the perm. copy of the aux. table.)
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS is_valid_route BOOL;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_modes TEXT[];"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS time_stamps TIMESTAMPTZ[];"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_sequence TTEXT(Sequence);")
                }
            };
            await using (var reader = await auxTableBatch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table creation",auxiliaryTable);
            }
        }

        public async Task PropagateResults(NpgsqlConnection connection, string routeTable, string auxiliaryTable)
        {
            await using var resultsBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);"), // Only for a quick visual review on TablePlus.
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);"),
                    new("UPDATE " + routeTable + " r_t SET route = aux_t.computed_route::tgeompoint FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id AND aux_t.is_valid_route = true;"),
                    new("UPDATE " + auxiliaryTable + " SET transport_sequence= coalesce_transport_modes_time_stamps(transport_modes, time_stamps) WHERE is_valid_route = true;"),
                    new("UPDATE " + routeTable + " r_t SET transport_sequence = aux_t.transport_sequence FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id;")
                }
            };
            await using (var reader = await resultsBatch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
            }
        }

        ///////////////////////////////////////////////////////////////
        //debug: Aux table copied to a permanent table for benchmarking
        public async Task SetComparisonTable(NpgsqlConnection connection, string auxiliaryTable, string comparisonTable)
        {
            //var comparisonTable=auxiliaryTable+"_comp";
            var comparisonTablePK=comparisonTable+"_PK";

            bool comparisonTableExists = false;
            await using (var prmCommand = new NpgsqlCommand("SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename  = '" + comparisonTable + "');", connection))
            await using (var reader = await prmCommand.ExecuteReaderAsync())
            {
                while(await reader.ReadAsync())
                {
                    comparisonTableExists = Convert.ToBoolean(reader.GetValue(0));
                }
            }

            if(!comparisonTableExists)
            {
                try
                {
                    await using var compTableBatch = new NpgsqlBatch(connection)
                    {
                        BatchCommands =
                        {
                            new("CREATE TABLE " + comparisonTable + " as (SELECT * FROM " + auxiliaryTable + ");"),
                            new("ALTER TABLE " + comparisonTable + " ADD CONSTRAINT " + comparisonTablePK + " PRIMARY KEY (persona_id);")
                        }
                    };

                    await using (await compTableBatch.ExecuteReaderAsync())
                    {
                        logger.Debug("{0} table creation",comparisonTable);
                    }
                }
                catch(Exception e)
                {
                    logger.Debug("Error creating comparison table '{0}: {1}", comparisonTable, e.Message);
                }
            }
            else
            {
                await using var cmd_insert = new NpgsqlCommand(@"
                INSERT INTO " + comparisonTable + @" (
                       persona_id, computed_route, transport_modes, time_stamps, transport_sequence, computed_route_2d, is_valid_route)
                SELECT persona_id, computed_route, transport_modes, time_stamps, transport_sequence, computed_route_2d, is_valid_route FROM " + auxiliaryTable + @" ON CONFLICT (persona_id) DO
                UPDATE SET 
                 computed_route = EXCLUDED.computed_route,
                 transport_modes = EXCLUDED.transport_modes,
                 time_stamps = EXCLUDED.time_stamps,
                 transport_sequence = EXCLUDED.transport_sequence,
                 computed_route_2d = EXCLUDED.computed_route_2d,
                 is_valid_route = EXCLUDED.is_valid_route;", connection);  
                
                await cmd_insert.ExecuteNonQueryAsync();
            }            
        }
        //:gudeb


        /////////////////////////////////////////////////////////////////////
        //debug: Routing results copied to a permanent table for benchmarking
        public async Task SetBenchmarkingTable(NpgsqlConnection connection, string auxiliaryTable, string benchmarkingTable, List<Persona> personas)
        {
            bool benchmarkingTableExists = false;
            await using (var prmCommand = new NpgsqlCommand("SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename  = '" + benchmarkingTable + "');", connection))
            await using (var reader = await prmCommand.ExecuteReaderAsync())
            {
                while(await reader.ReadAsync())
                {
                    benchmarkingTableExists = Convert.ToBoolean(reader.GetValue(0));
                }
            }

            if(!benchmarkingTableExists)
            {
                logger.Debug("Benchmarking table not found: {0}",benchmarkingTable);
            }
            else
            {
                await using var cmd_insert = new NpgsqlCommand(@"
                    INSERT INTO " + benchmarkingTable + @" (
                        id, computed_route, computed_route_2d, transport_modes, time_stamps, is_valid_route)
                    SELECT persona_id as id, computed_route, computed_route_2d, transport_modes, time_stamps, is_valid_route FROM " + auxiliaryTable + @" ON CONFLICT (id) DO
                    UPDATE SET 
                    computed_route = EXCLUDED.computed_route,
                    computed_route_2d = EXCLUDED.computed_route_2d,
                    transport_modes = EXCLUDED.transport_modes,
                    time_stamps = EXCLUDED.time_stamps,
                    is_valid_route = EXCLUDED.is_valid_route;", connection);  
                
                await cmd_insert.ExecuteNonQueryAsync();
            

                int uploadFails = 0;
                foreach(var persona in personas)
                {
                    try
                    {
                        var route = persona.Route;
                        double lastTime = -1.0;
                        if(route is not null)
                        {
                            var routeCoordinates=route.Coordinates;
                            lastTime = routeCoordinates.Last().M;
                        }
                            
                        var timeStampsTZ = persona.TTextTransitions.Item2;
                        var interval = TimeSpan.FromSeconds(lastTime);
                        
                        await using var cmd_insert_ttext = new NpgsqlCommand("INSERT INTO " + benchmarkingTable + " (id, total_time, total_time_interval) VALUES ($1, $2, $3) ON CONFLICT (id) DO UPDATE SET total_time = $2, total_time_interval = $3", connection)
                        {
                            Parameters =
                            {
                                new() { Value = persona.Id },
                                new() { Value = timeStampsTZ.Last() },
                                new() { Value = interval },
                            }
                        };
                    
                        await cmd_insert_ttext.ExecuteNonQueryAsync();
                    }
                    catch (Exception e)
                    {
                        logger.Debug(" ==>> Unable to upload route to database. Persona Id {0}: {0}", persona.Id, e.Message);
                        uploadFails++;
                    }
                }
            }
        }
        //:gudeb

        

        public virtual Task UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas,  string comparisonTable = "", string benchmarkingTable = "")
        {
           throw new NotImplementedException();
        }
    }
}