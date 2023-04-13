using System.Diagnostics;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NLog;
using Npgsql;
using SytyRouting.Gtfs.ModelGtfs;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public class DebugGeometryUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public async Task SetDebugGeomTable(string connectionString, string debugGeomTable)
        {
            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            var debugGeomTablePK = debugGeomTable+"_PK";

            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + debugGeomTable + " CASCADE;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using var debugTableBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("CREATE TABLE IF NOT EXISTS " + debugGeomTable + " (id TEXT);"),
                    new("ALTER TABLE " + debugGeomTable + " DROP CONSTRAINT IF EXISTS " + debugGeomTablePK + ";"),
                    new("ALTER TABLE " + debugGeomTable + " ADD CONSTRAINT " + debugGeomTablePK + " PRIMARY KEY (id);"),
                    new("ALTER TABLE " + debugGeomTable + " ADD COLUMN IF NOT EXISTS trajectory GEOMETRY;"),
                    new("ALTER TABLE " + debugGeomTable + " ADD COLUMN IF NOT EXISTS trajectory_2d GEOMETRY;"), // uncomment this line to get a quick view of the routing trajectory on TablePlus. (Modify also resultsBatch and the perm. copy of the aux. table.)
                    new("ALTER TABLE " + debugGeomTable + " ADD COLUMN IF NOT EXISTS is_valid_trajectory BOOL;")
                }
            };
            await using (var reader = await debugTableBatch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table creation",debugGeomTable);
            }
        }

        public async Task UploadTrajectoriesAsync(string connectionString, string debugGeomTable, List<ShapeGtfs> trajectories)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            using var importer = connection.BeginBinaryImport("COPY " + debugGeomTable + " (id, trajectory) FROM STDIN (FORMAT binary)");
            foreach (var trajectory in trajectories)
            {
                await importer.StartRowAsync();
                await importer.WriteAsync(trajectory.Id);
                await importer.WriteAsync(trajectory.LineString);
            }
            await importer.CompleteAsync();
            await importer.CloseAsync();
            
            ///
            await PropagateResults(connection,debugGeomTable);
            ///

            
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Debug("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
            logger.Debug("   Route uploading execution time :: {0}", totalTime);
            logger.Debug("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
        }

        public async Task UploadTrajectoriesAsync(string connectionString, string debugGeomTable, List<KeyValuePair<string,LineString>> trajectories)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            using var importer = connection.BeginBinaryImport("COPY " + debugGeomTable + " (id, trajectory) FROM STDIN (FORMAT binary)");
            int trajectoryId = 0;
            foreach (var trajectory in trajectories)
            {
                await importer.StartRowAsync();
                await importer.WriteAsync(trajectory.Key+"_"+trajectoryId);
                await importer.WriteAsync(trajectory.Value);
                ++trajectoryId;
            }
            await importer.CompleteAsync();
            await importer.CloseAsync();
            
            ///
            await PropagateResults(connection,debugGeomTable);
            ///

            
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Debug("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
            logger.Debug("   Route uploading execution time :: {0}", totalTime);
            logger.Debug("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
        }

        public async Task PropagateResults(NpgsqlConnection connection, string debugGeomTable)
        {
            await using var resultsBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + debugGeomTable + " SET trajectory_2d = st_force2d(trajectory);"), // Only for a quick visual review on TablePlus.
                    new("UPDATE " + debugGeomTable + " SET is_valid_trajectory = st_IsValidTrajectory(trajectory);"),
                    new("UPDATE " + debugGeomTable + " SET is_valid_trajectory = false WHERE st_IsEmpty(trajectory);")
                }
            };
            await using (var reader = await resultsBatch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",debugGeomTable);
            }
        }
    }
}