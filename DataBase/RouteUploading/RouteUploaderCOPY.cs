using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class RouteUploaderCOPY : BaseRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public override async Task UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas, string comparisonTable)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            var auxiliaryTable=routeTable+Configuration.AuxiliaryTableSuffix;
            var auxiliaryTablePK=auxiliaryTable+Configuration.PKConstraintSuffix;

            ///
            await SetAuxiliaryTable(connection,auxiliaryTable,auxiliaryTablePK);
            ///

            using var importer = connection.BeginBinaryImport("COPY " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) FROM STDIN (FORMAT binary)");
            foreach (var persona in personas)
            {
                await importer.StartRowAsync();
                await importer.WriteAsync(persona.Id);
                await importer.WriteAsync(persona.Route);
                await importer.WriteAsync(persona.TTextTransitions.Item1);
                await importer.WriteAsync(persona.TTextTransitions.Item2);
            }
            await importer.CompleteAsync();
            await importer.CloseAsync();
            
            ///
            await PropagateResults(connection,routeTable,auxiliaryTable);
            ///

            ///////////////////////////////////////////////////////////////
            //debug: Aux table copied to a permanent table for benchmarking
            await SetComparisonTable(connection,auxiliaryTable,comparisonTable);
            //:gudeb

  
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Debug("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
            logger.Debug("   Route uploading execution time :: {0}", totalTime);
            logger.Debug("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
        }
    }
}