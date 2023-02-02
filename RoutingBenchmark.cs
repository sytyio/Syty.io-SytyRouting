using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting
{
    public static class RoutingBenchmark
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static Stopwatch stopWatch = new Stopwatch();
        

        public static async Task CreateDataSet()
        {
            stopWatch.Start();
            
            await PersonaLocalDBUploadAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Routing benchmark data set creation time :: {0}", totalTime);
        }

        private static async Task PersonaLocalDBUploadAsync()
        {
            Stopwatch uploadStopWatch = new Stopwatch();
            uploadStopWatch.Start();

            //var connectionString = Configuration.LocalConnectionString;  // Local DB for testing
            var connectionString = Configuration.ConnectionString;  // Local DB for testing

            // Selected points around Brussels:

            //                             Domicile						Travail
            //                         X		Y						                X		Y
            // // Anderlecht 		4.2943		50.83157		Avenue Louise		4.36927		50.82102
            // Rue de la loi 		4.3643		50.84645		Altitude 100		4.33757		50.81635 
            // Hopital Erasme		4.26710		50.81541		Blvd Lemonnier		4.34154		50.84040
            // La bourse 		    4.34965		50.84825		Filigranes		    4.36790  	50.84353
            // Avenue d'itterbeek	4.27223		50.83618		Blvd Anspach		4.35171		50.85046
            // Avenue Chazal 		4.39112		50.85875		Rue Stevin		    4.37673		50.84564


            var routingProbes = Configuration.RoutingProbes;
            List<Persona> realBrusselVloms = new List<Persona>(routingProbes.Length);

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();

            for(int i=0; i<routingProbes.Length; i++)
            {
                Point home = geometryFactory.CreatePoint(new Coordinate(routingProbes[i].HomeLongitude, routingProbes[i].HomeLatitude));
                Point work = geometryFactory.CreatePoint(new Coordinate(routingProbes[i].WorkLongitude, routingProbes[i].WorkLatitude));
                byte[] requestedTransportModeSequence = TransportModes.NamesToArray(routingProbes[i].TransportSequence);

                realBrusselVloms.Add(new Persona {Id = i+1, HomeLocation = home, WorkLocation = work, RequestedTransportSequence = requestedTransportModeSequence});

                Console.WriteLine("From {0} to {1} by {2}",routingProbes[i].HomeLocation,routingProbes[i].WorkLocation,TransportModes.NamesToString(TransportModes.ArrayToNames(requestedTransportModeSequence)));
                Console.WriteLine("{0}, {1}",routingProbes[i].HomeLatitude,routingProbes[i].HomeLongitude);
                Console.WriteLine("{0}, {1}",routingProbes[i].WorkLatitude,routingProbes[i].WorkLongitude);
            }

            // For a batch selection from a bounding box on Brussels:
            // Refs.: https://wiki.openstreetmap.org/wiki/Bounding_Box
            //        https://norbertrenner.de/osm/bbox.html

            // box:
            // comma	
            // 4.2491,50.7843,4.4887,50.9229
            // (left,bottom,right,top) -
            // api, xapi, mapnik, maperitive, osmconvert
            // space	
            // 4.2491 50.7843 4.4887 50.9229
            // (left bottom right top)
            // osmosis	
            // left=4.2491 bottom=50.7843 right=4.4887 top=50.9229
            // overpass ql	
            // (50.7843,4.2491,50.9229,4.4887)
            // overpass xml	
            // <bbox-query e="4.4887" n="50.9229" s="50.7843" w="4.2491"/>
            // wkt	
            // POLYGON((4.2491 50.7843,4.4887 50.7843,4.4887 50.9229,4.2491 50.9229,4.2491 50.7843))
            // osm.org	
            // http://www.openstreetmap.org/?&box=yes& ...


            // Ref.: MobilityDB 1.0 User’s Manual, p.45
            // A common way to store temporal points in PostGIS is to represent them as geometries of type LINESTRING M and abuse the
            // M dimension to encode timestamps as seconds since 1970-01-01 00:00:00. These time-enhanced geometries, called trajectories,
            // can be validated with the function ST_IsValidTrajectory to verify that the M value is growing from each vertex to
            // the next. Trajectories can be manipulated with the functions ST_ClosestPointOfApproach, ST_DistanceCPA, and
            // ST_CPAWithin. Temporal point values can be converted to/from PostGIS trajectories
            //
            // Cast a PostGIS trajectory to a temporal point:
            // geometry::tgeompoint
            // geography::tgeogpoint

            // Ref.: PostGIS:
            // https://postgis.net/docs/ST_IsValidTrajectory.html
            // boolean ST_IsValidTrajectory(geometry line);
            // Tests if a geometry encodes a valid trajectory. A valid trajectory is represented as a LINESTRING with measures (M values). The measure values must increase from each vertex to the next. 

            // https://postgis.net/docs/ST_IsEmpty.html                       
            // boolean ST_IsEmpty(geometry geomA);
            // Returns true if this Geometry is an empty geometry. If true, then this Geometry represents an empty geometry collection, polygon, point etc.
            
            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            var personaTable = Configuration.PersonaTable;
            var routingBenchmarkTable = Configuration.RoutingBenchmarkTable;
            var additionalProbes = Configuration.AdditionalRoutingProbes;

            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + routingBenchmarkTable + ";", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("CREATE TABLE " + routingBenchmarkTable + " AS SELECT id, home_location, work_location FROM " + personaTable + " WHERE id > " + routingProbes.Length + " AND ST_X(home_location) > 4.2491 AND ST_X(home_location) < 4.4887 AND ST_Y(home_location) > 50.7843 AND ST_Y(home_location) < 50.9229 AND ST_X(work_location) > 4.2491 AND ST_X(work_location) < 4.4887 AND ST_Y(work_location) > 50.7843 AND ST_Y(work_location) < 50.9229 ORDER BY id ASC OFFSET " + routingProbes.Length + " LIMIT " + additionalProbes + ";", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " DROP CONSTRAINT IF EXISTS routingbenchmarktest_pk;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD CONSTRAINT routingbenchmarktest_pk PRIMARY KEY (id);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS transport_sequence TEXT[];", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS computed_route GEOMETRY;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS computed_route_2d GEOMETRY;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS total_time TIMESTAMPTZ;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS total_time_interval INTERVAL;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS is_valid_route BOOL;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS computed_route_temporal_point TGEOMPOINT;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS transport_modes TEXT[];", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS time_stamps TIMESTAMPTZ[];", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTable + " ADD COLUMN IF NOT EXISTS transport_transitions TTEXT(Sequence);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // PLGSQL: Merge each transport_mode with its corresponding transition time_stamp: 
            var functionString =  @"
            CREATE OR REPLACE FUNCTION coalesce_transport_modes_time_stamps(transport_modes text[], time_stamps timestamptz[]) RETURNS ttext(Sequence) AS $$
            DECLARE
            _arr_ttext ttext[];
            _seq_ttext ttext(Sequence);
            _transport_mode text;
            _index int;
            BEGIN
                _index := 0;
                FOREACH _transport_mode IN ARRAY transport_modes
                LOOP
                    _index := _index + 1;
                    RAISE NOTICE 'current tranport mode: %', _transport_mode;
                    _arr_ttext[_index] := ttext_inst(transport_modes[_index], time_stamps[_index]);            
                    RAISE NOTICE 'current ttext: %', _arr_ttext[_index];
                END LOOP;
                _seq_ttext := ttext_seq(_arr_ttext);
                RAISE NOTICE 'sequence: %', _seq_ttext;
                RETURN _seq_ttext;
                EXCEPTION
                    WHEN others THEN
                        RAISE NOTICE 'An error has occurred:';
                        RAISE NOTICE '% %', SQLERRM, SQLSTATE;
                        --RETURN ttext_seq(ARRAY[ttext_inst('None', '1970-01-01 00:00:00'), ttext_inst('None', '1970-01-01 00:00:01')]);
                        RETURN null;
            END;
            $$ LANGUAGE PLPGSQL;
            ";

            await using (var cmd = new NpgsqlCommand(functionString, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            int uploadFails = 0;

            foreach(var brusseleir in realBrusselVloms)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routingBenchmarkTable + " (id, home_location, work_location, transport_sequence) VALUES ($1, $2, $3, $4) ON CONFLICT (id) DO UPDATE SET home_location = $2, work_location = $3, transport_sequence = $4", connection)
                    {
                        Parameters =
                        {
                            new() { Value = brusseleir.Id },
                            new() { Value = brusseleir.HomeLocation },
                            new() { Value = brusseleir.WorkLocation },
                            new() { Value = TransportModes.ArrayToNames(brusseleir.RequestedTransportSequence) }
                        }
                    };
                    await cmd_insert.ExecuteNonQueryAsync();
                }
                catch
                {
                    logger.Debug(" ==>> Unable to upload record to database. Persona Id {0}", brusseleir.Id);
                    uploadFails++;
                }
            }

            int[] additionalProbesIds = new int[additionalProbes];
            var queryString = "SELECT id FROM " + routingBenchmarkTable + " WHERE id > " + routingProbes.Length + ";";
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                int i = 0;
                while (await reader.ReadAsync() && i < additionalProbes)
                {
                    additionalProbesIds[i++] = Convert.ToInt32(reader.GetValue(0));
                }
            }
   
            for(int i = 0; i < additionalProbes; i++)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routingBenchmarkTable + " (id, transport_sequence) VALUES ($1, $2) ON CONFLICT (id) DO UPDATE SET transport_sequence = $2", connection)
                    {
                        Parameters =
                        {
                            new() { Value = additionalProbesIds[i] },
                            new() { Value = Configuration.DefaultBenchmarkSequence }
                        }
                    };
                    await cmd_insert.ExecuteNonQueryAsync();
                }
                catch
                {
                    logger.Debug(" ==>> Unable to upload record to database. Persona Id {0}", additionalProbesIds[i]);
                    uploadFails++;
                }
            }
            
            await connection.CloseAsync();

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Info("{0} Persona examples successfully uploaded to the database (routing benchmark table) in {1} (d.hh:mm:s.ms)", realBrusselVloms.Count - uploadFails,  totalTime);
            var totalDBItems = await Helper.DbTableRowCount(Configuration.RoutingBenchmarkTable, logger);
            logger.Debug("{0} personas (out of {1}) failed to upload ({2} %) to the routing benchmark table", uploadFails, totalDBItems, 100 * uploadFails / totalDBItems);
        }
    }
}
