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
        

        public static async Task StartReplication()
        {
            stopWatch.Start();
            
            await PersonaLocalDBUploadAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Persona Replication time :: {0}", totalTime);
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
                byte[] requestedTransportModeSequence = TransportModes.NameSequenceToMasksArray(routingProbes[i].TransportSequence);

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

            await using (var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS " + routingBenchmarkTableName + " AS SELECT id, home_location, work_location FROM " + personaTableName + " WHERE ST_X(home_location) > 4.2491 AND ST_X(home_location) < 4.4887 AND ST_Y(home_location) > 50.7843 AND ST_Y(home_location) < 50.9229 AND ST_X(work_location) > 4.2491 AND ST_X(work_location) < 4.4887 AND ST_Y(work_location) > 50.7843 AND ST_Y(work_location) < 50.9229 ORDER BY id ASC OFFSET " + routingProbes.Length + " LIMIT " + additionalProbes + ";", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " DROP CONSTRAINT IF EXISTS routingBenchmark_pk;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routingBenchmarkTableName + " ADD CONSTRAINT routingBenchmark_pk PRIMARY KEY (id);", connection))
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
   
            
            await connection.CloseAsync();

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Info("{0} Persona examples successfully uploaded to the database in {1} (d.hh:mm:s.ms)", realBrusselVloms.Count - uploadFails,  totalTime);
            var totalDBItems = await Helper.DbTableRowCount(Configuration.RoutingBenchmarkTableName, logger);
            logger.Debug("{0} personas (out of {1}) failed to upload ({2} %)", uploadFails, totalDBItems, 100 * uploadFails / totalDBItems);
        }
    }
}
