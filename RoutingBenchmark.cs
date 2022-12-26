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

            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            var personaTableName = Configuration.PersonaTableName;
            var routingBenchmarkTableName = Configuration.RoutingBenchmarkTableName;
            var additionalProbes = Configuration.AdditionalRoutingProbes;

            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + routingBenchmarkTableName + ";", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("CREATE TABLE " + routingBenchmarkTableName + " AS SELECT id, home_location, work_location FROM " + personaTableName + " WHERE id > " + routingProbes.Length + " AND ST_X(home_location) > 4.2491 AND ST_X(home_location) < 4.4887 AND ST_Y(home_location) > 50.7843 AND ST_Y(home_location) < 50.9229 AND ST_X(work_location) > 4.2491 AND ST_X(work_location) < 4.4887 AND ST_Y(work_location) > 50.7843 AND ST_Y(work_location) < 50.9229 ORDER BY id ASC OFFSET " + routingProbes.Length + " LIMIT " + additionalProbes + ";", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " DROP CONSTRAINT IF EXISTS routingbenchmark_pk;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD CONSTRAINT routingbenchmark_pk PRIMARY KEY (id);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD COLUMN IF NOT EXISTS transport_sequence TEXT[];", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD COLUMN IF NOT EXISTS computed_route GEOMETRY;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD COLUMN IF NOT EXISTS computed_route_2d GEOMETRY;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD COLUMN IF NOT EXISTS computed_route_total_milliseconds GEOMETRY;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD COLUMN IF NOT EXISTS computed_route_temporal_point_1 TGEOMPOINT;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD COLUMN IF NOT EXISTS computed_route_temporal_point_2 TGEOMPOINT;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            int uploadFails = 0;

            foreach(var brusseleir in realBrusselVloms)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routingBenchmarkTableName + " (id, home_location, work_location, transport_sequence) VALUES ($1, $2, $3, $4) ON CONFLICT (id) DO UPDATE SET home_location = $2, work_location = $3, transport_sequence = $4", connection)
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
            var queryString = "SELECT id FROM " + routingBenchmarkTableName + " WHERE id > " + routingProbes.Length + ";";
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
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routingBenchmarkTableName + " (id, transport_sequence) VALUES ($1, $2) ON CONFLICT (id) DO UPDATE SET transport_sequence = $2", connection)
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

            // Ref.: MobilityDB 1.0 User’s Manual, p.45
            // A common way to store temporal points in PostGIS is to represent them as geometries of type LINESTRING M and abuse the
            // M dimension to encode timestamps as seconds since 1970-01-01 00:00:00. These time-enhanced geometries, called trajectories,
            // can be validated with the function ST_IsValidTrajectory to verify that the M value is growing from each vertex to
            // the next. Trajectories can be manipulated with the functions ST_ClosestPointOfApproach, ST_DistanceCPA, and
            // ST_CPAWithin. Temporal point values can be converted to/from PostGIS trajectories

            // Cast a PostGIS trajectory to a temporal point:
            // geometry::tgeompoint
            // geography::tgeogpoint
            // SELECT asText(geometry 'LINESTRING M (0 0 978307200,0 1 978393600,
            // 1 1 978480000)'::tgeompoint);
            // -- "[POINT(0 0)@2001-01-01, POINT(0 1)@2001-01-02, POINT(1 1)@2001-01-03]";
            // SELECT asText(geometry 'GEOMETRYCOLLECTION M (LINESTRING M (0 0 978307200,1 1 978393600),
            // POINT M (1 1 978480000),LINESTRING M (1 1 978652800,0 0 978739200))'::tgeompoint);
            // -- "{[POINT(0 0)@2001-01-01, POINT(1 1)@2001-01-02], [POINT(1 1)@2001-01-03],
            // [POINT(1 1)@2001-01-05, POINT(0 0)@2001-01-06]}"
            
            await connection.CloseAsync();

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Info("{0} Persona examples successfully uploaded to the database in {1} (d.hh:mm:s.ms)", realBrusselVloms.Count - uploadFails,  totalTime);
            var totalDBItems = await Helper.DbTableRowCount(Configuration.RoutingBenchmarkTableName, logger);
            logger.Debug("{0} personas (out of {1}) failed to upload ({2} %)", uploadFails, totalDBItems, 100 * uploadFails / totalDBItems);
        }
    }
}
