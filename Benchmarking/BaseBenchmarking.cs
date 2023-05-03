using System.Diagnostics.CodeAnalysis;
using NLog;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using Npgsql;
using NetTopologySuite.Utilities;
using System.Diagnostics;
using SytyRouting.DataBase;
using SytyRouting.Routing;

namespace SytyRouting.Algorithms
{
    public abstract class BaseBenchmarking
    {
        [NotNull]
        protected static Graph? _graph = null!;

        public static List<TimeSpan> totalTimes = new List<TimeSpan>();
        public static List<TimeSpan> routingTimes = new List<TimeSpan>();
        public static List<TimeSpan> uploadingTimes = new List<TimeSpan>();
        public static List<TimeSpan> downloadingTimes = new List<TimeSpan>();
        public static List<string> uploadResults = new List<string>();
        public static List<string> comparisonResults = new List<string>();
        public static List<string> tableNames = new List<string>();
        public static List<string> compTableNames = new List<string>();
        public static List<string> downloadStrategies = new List<string>();


        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> CheckUploadedRoutesAsync(List<Persona> personas, string routeTable, int computedRoutes)
        {
            logger.Info("DB uploaded routes integrity check:");
            int numberOfFailures = 0;
            if(personas!=null)
            {
                var uploadedPersonas = new Dictionary<int, LineString>(personas.Count);

                //var connectionString = Configuration.X270ConnectionString; // Local DB for testing
                var connectionString = Configuration.ConnectionString;

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                // Read location data from 'persona routes'
                //                     0              1             
                var queryString = "SELECT persona_id, computed_route FROM " + routeTable + " ORDER BY persona_id ASC";

                await using (var command = new NpgsqlCommand(queryString, connection))
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var persona_id = Convert.ToInt32(reader.GetValue(0)); // persona_id (int)
                        try
                        {
                            var route = (LineString)reader.GetValue(1); // route (Point)
                            if(!route.IsEmpty)
                                uploadedPersonas.Add(persona_id, route);
                        }
                        catch
                        {
                            logger.Debug("Unable to download route for Persona Id {0}", persona_id);
                            numberOfFailures++;
                        }
                    }
                }

                if (computedRoutes != uploadedPersonas.Count)
                {
                    logger.Debug("Inconsistent number of routes in the database");
                }
                logger.Debug("Computed routes (non-empty) {0} : {1} Non-empty routes in the database", computedRoutes, uploadedPersonas.Count);

                foreach (var persona in personas)
                {
                    if(persona.Route is null || persona.Route.IsEmpty)
                    {
                        logger.Debug("Invalid/empty route for Persona Id {0}", persona.Id);
                        continue;
                    }

                    if (uploadedPersonas.ContainsKey(persona.Id))
                    {
                        try
                        {
                            Assert.IsEquals(uploadedPersonas[persona.Id], persona.Route, "Test failed. Original and Uploaded Routes are not equal");
                        }
                        catch (NetTopologySuite.Utilities.AssertionFailedException e)
                        {
                            logger.Debug("Route equality assertion failed for Persona Id {0}: {1}", persona.Id, e.Message);
                            numberOfFailures++;
                        }
                    }
                    else
                    {
                        logger.Debug("Unable to compare routes for Persona Id {0}", persona.Id);
                        //TracePersonaDetails(persona);
                        //numberOfFailures++;
                    }
                }
                await connection.CloseAsync();
            }
            else
            {
                numberOfFailures++;
            }

            string result;
            if (numberOfFailures != 0)
                result = "FAILED";
            else
                result = "SUCCEEDED";

            logger.Info("-------------------------------------------------------------");
            logger.Info("DB uploaded route integrity check {0} for valid routes.", result);
            logger.Info("-------------------------------------------------------------");

            return result;
        }

        public static async Task<string> CompareUploadedRoutesAsync(string firstRouteTable, string secondRouteTable)
        {
            logger.Info("--------------------------------------------------------------------------------------------------------------");
            logger.Info("DB uploaded routes comparison (LineString)");
            logger.Info("Route tables: {0} vs. {1}",firstRouteTable,secondRouteTable);
            logger.Info("--------------------------------------------------------------------------------------------------------------");
            
            int numberOfFailures = 0;

            var elementsFirstTable = await Helper.DbTableRowCount(firstRouteTable, logger);
            var elementsSecondTable = await Helper.DbTableRowCount(secondRouteTable, logger);

            if(elementsFirstTable!=elementsSecondTable)
            {
                logger.Debug("Incompatible number of elements: {0} != {1}",elementsFirstTable,elementsSecondTable);
            }
            else
            {
                logger.Debug("{0} routes to compare",elementsFirstTable);
            }

            //var connectionString = Configuration.X270ConnectionString; // Local DB for testing
            var connectionString = Configuration.ConnectionString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            LineString[] routesFirstTable = new LineString[elementsFirstTable];
            LineString[] routesSecondTable = new LineString[elementsSecondTable];

            // Read location data from 'persona routes'
            //                     0              1             
            var queryString = "SELECT persona_id, computed_route FROM " + firstRouteTable + " ORDER BY persona_id ASC";

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                int i=0;
                while (await reader.ReadAsync())
                {
                    var persona_id = Convert.ToInt32(reader.GetValue(0)); // persona_id (int)
                    try
                    {
                        var route = (LineString)reader.GetValue(1); // route (Point)
                        routesFirstTable[i] = route;
                    }
                    catch
                    {
                        logger.Debug("Unable to download route for Persona Id {0}", persona_id);
                        numberOfFailures++;
                    }
                    i++;
                }
            }

            // Read location data from 'persona routes'
            //                     0              1             
            queryString = "SELECT persona_id, computed_route FROM " + secondRouteTable + " ORDER BY persona_id ASC";

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                int i=0;
                while (await reader.ReadAsync())
                {
                    var persona_id = Convert.ToInt32(reader.GetValue(0)); // persona_id (int)
                    try
                    {
                        var route = (LineString)reader.GetValue(1); // route (Point)
                        routesSecondTable[i] = route;
                    }
                    catch
                    {
                        logger.Debug("Unable to download route for Persona Id {0}", persona_id);
                        numberOfFailures++;
                    }
                    i++;
                }
            }

            if(routesFirstTable.Length!=routesSecondTable.Length)
            {
                logger.Debug("Incompatible number of elements: {0} != {1}",elementsFirstTable,elementsSecondTable);
            }
            else
            {
                for(int i=0; i<routesFirstTable.Length; i++)
                {
                    if(routesFirstTable[i]!=null && routesSecondTable[i]!=null)
                    {
                        try
                        {
                            Assert.IsEquals(routesFirstTable[i], routesSecondTable[i], "Test failed. Uploaded Routes are not equal");
                        }
                        catch (NetTopologySuite.Utilities.AssertionFailedException e)
                        {
                            logger.Debug("Route equality assertion failed index {0}: {1}", i, e.Message);
                            numberOfFailures++;
                        }
                    }
                    else
                    {
                        logger.Debug("Invalid route");
                        numberOfFailures++;
                    }
                }
            }

            string result;
            if (numberOfFailures != 0)
                result = "FAILED";
            else
                result = "SUCCEEDED";

            logger.Info("---------------------------------------------");
            logger.Info("DB uploaded routes comparison {0}.", result);
            logger.Info("---------------------------------------------");

            await connection.CloseAsync();

            return result;
        }

        private static async Task CleanComparisonTablesAsync(string connectionString, List<string> comparisonTables)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            foreach(var table in comparisonTables)
            {
                await using var cmd_drop = new NpgsqlCommand("DROP TABLE IF EXISTS " + table + ";", connection);
                await cmd_drop.ExecuteNonQueryAsync();
            }

            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Debug("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
            logger.Debug("   Cleaning database execution time :: {0}", totalTime);
            logger.Debug("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        }

        private static void TracePersonaDetails(Persona persona)
        {
            var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
            var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
            logger.Debug("Persona details:");
            logger.Debug("Id {0}", persona.Id);
            logger.Debug("Home location: ({0,18},{1,18})\t :: OSM Coordinates: {2,18},{3,18}\t : Origin OsmID      {4}", persona.HomeLocation!.X, persona.HomeLocation!.Y, persona.HomeLocation!.Y, persona.HomeLocation!.X, origin.OsmID);
            var originTransportModes = origin.GetAvailableOutboundTransportModes();
            logger.Debug("Avilable Outbound Transport modes for Node {0}: {1}", origin.OsmID, TransportModes.MaskToString(originTransportModes));
            logger.Debug("Work location: ({0,18},{1,18})\t :: OSM Coordinates: {2,18},{3,18}\t : Destination OsmID {4}", persona.WorkLocation!.X, persona.WorkLocation!.Y, persona.WorkLocation!.Y, persona.WorkLocation!.X, destination.OsmID);
            var destinationTransportModes = origin.GetAvailableInboundTransportModes();
            logger.Debug("Avilable Inbound Transport modes for Node {0}: {1}", destination.OsmID, TransportModes.MaskToString(originTransportModes));
            logger.Debug("Requested transport modes: {0} ({1})", TransportModes.NamesToString(TransportModes.ArrayToNames(persona.RequestedTransportSequence)), TransportModes.ArrayToMask(persona.RequestedTransportSequence));
        }

        public void TracePersonasRouteResult(List<Persona> personas)
        {
            int routeComputationFails = 0;
            foreach (var persona in personas)
            {
                if(persona.SuccessfulRouteComputation is not true)
                {
                    logger.Debug("Persona: Id = {0}, route found = {1}", persona.Id, persona.SuccessfulRouteComputation);
                    TracePersonaDetails(persona);
                    routeComputationFails++;
                }
            }
            logger.Debug("{0} routes missing", routeComputationFails);
        }

        public void TracePersonas(List<Persona> personas)
        {
            logger.Debug("");
            logger.Debug("Personas:");
            foreach (var persona in personas)
            {
                var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                logger.Debug("");
                logger.Debug("Id {0}:\t HomeLocation = {1}:({2}, {3}),\t WorkLocation = {4}:({5}, {6})",
                    persona.Id, origin.OsmID, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                                destination.OsmID, persona.WorkLocation?.X, persona.WorkLocation?.Y);
                
                TraceRoute(persona);
            }
        }

        public static void TraceRoute(Persona persona)
        {
            //if(persona.Route is not null && persona.TransportModeTransitions is not null)
            if(persona.Route is not null && persona.TTextTransitions is not null)
            {
                // TraceRoute(persona.Route);
                //TraceRouteDetails(persona.Route, persona.TransportModeTransitions);
                TraceRouteDetails(persona.Route, persona.TTextTransitions);
                // TransportTransitionsToTTEXT(persona.Route, persona.TransportModeTransitions);
            }
        }

        public void TraceRoute(LineString route)
        {
            var routeCoordinates = route.Coordinates;

            Node node;
            string timeStamp;

            logger.Debug("> Route ({0})", routeCoordinates.Length);
            string routeNodeString      = String.Format(">            Nodes:      ");
            string routeTimeStampString = String.Format(">            Time stamps:");
            for(var n = 0; n < routeCoordinates.Length; n++)
            {
                node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);
                timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[n].M)); // <- debug: check units
                routeNodeString += String.Format(" {0,14},", node.OsmID);
                routeTimeStampString += String.Format(" {0,14},", timeStamp);
                if(n>2)
                {
                    break;
                }  
            }
            node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[route.Count -1].X, routeCoordinates[route.Count -1].Y);
            timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M)); // <- debug: check units
            routeNodeString += String.Format(" ..., {0,14} ", node.OsmID);
            routeTimeStampString += String.Format(" ..., {0,14} ", timeStamp);
            logger.Debug(routeNodeString);
            logger.Debug(routeTimeStampString);
        }

        public void TraceFullRoute(LineString route)
        {
            var routeCoordinates = route.Coordinates;
            
            logger.Debug("GeometryType:", route.GeometryType);
            logger.Debug("IsClosed: {0}", route.IsClosed);
            logger.Debug("IsEmpy: {0}", route.IsEmpty);
            logger.Debug("IsGeometryCollection: {0}", route.IsSimple);
            logger.Debug("IsValid: {0}", route.IsValid);

            Node node;
            string timeStamp;

            logger.Debug("> Route ({0})", routeCoordinates.Length);
            logger.Debug(String.Format(" Index :    --------- Coordinates ----------      ::                Node  ::                   M ::         Time stamp"));
            logger.Debug(String.Format("       :                  X ::                  Y ::                      ::                     ::                   "));

            double previousM=-1.0;
            double  currentM=0.0;
            for(var n = 0; n < routeCoordinates.Length; n++)
            {
                currentM = routeCoordinates[n].M;
                node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);
                if(route.Coordinates[n].M<double.MaxValue)
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[n].M)); // <- debug: check units
                else
                    timeStamp = "Inf <<<===";
                if(previousM>=currentM)
                    timeStamp = " " + timeStamp + " <<<=== M ordinate inconsistency"; 
                logger.Debug(String.Format("{0,6} : {1,18} :: {2,18} :: {3,20} :: {4,20} :: {5,15}",n+1,routeCoordinates[n].X,routeCoordinates[n].Y, node.OsmID, routeCoordinates[n].M, timeStamp));
                previousM = currentM;
            }

            //Environment.Exit(0);
        }

        public static void TraceRouteDetails(LineString route, Dictionary<int, Tuple<byte,int>>? transportModeTransitions)
        {
            var routeCoordinates = route.Coordinates;

            Node node;
            string timeStamp;

            if(transportModeTransitions!=null)
            {
                try{
                    foreach(var transportModeTransition in transportModeTransitions)
                    {
                       logger.Debug("Transport Mode transitions :: {0}:{1}: {2}", transportModeTransition.Key, transportModeTransition.Value, TransportModes.MaskToString(transportModeTransition.Value.Item1));
                    }

                    logger.Debug("> Route ({0} vertices)", routeCoordinates.Length);
                    if(routeCoordinates.Length<1)
                    {
                        logger.Debug("> Empty route");
                        return;
                    }
                    string vertexS        = String.Format("{0,8}","Vertex");
                    string nodeS          = String.Format("{0,20}","Node OSM Id");
                    string timeStampS     = String.Format("{0,14}","Time stamp");
                    string coordinateXS   = String.Format("{0,20}","Coordinate X");
                    string coordinateYS   = String.Format("{0,20}","Coordinate Y");
                    string transportModeS = String.Format("{0,18}","Transport Mode");
                    string routeTypeS     = String.Format("{0,10}","Route Type");
                    string routeTagS      = String.Format("{0,30}","Route Tag (Value : Key)");
                    string routeTransportModesS      = String.Format(" {0,30}","Route Allowed Transport Modes");
                    string nodeIdxS       = String.Format(" {0,14}","Node Idx");
                    logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                    logger.Debug("=======================================================================================================================================================================================================================");
                    
                    int transportModeRepetitions=0;
                    byte currentTransportMode = 0;
                    byte previousTransportMode = 0;
                    for(var n = 0; n < routeCoordinates.Length-1; n++)
                    {
                        node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);

                        if(transportModeTransitions.ContainsKey(node.Idx))
                        {
                            currentTransportMode = transportModeTransitions[node.Idx].Item1;
                        }

                        if(previousTransportMode!=currentTransportMode)
                        {
                            previousTransportMode = currentTransportMode;    
                            transportModeS = String.Format("{0,18}",TransportModes.MaskToString(currentTransportMode));
                            var routeType = transportModeTransitions[node.Idx].Item2;
                            routeTypeS     = String.Format("{0,10}",routeType);
                            if(TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                                routeTagS      = String.Format("{0,30}",TransportModes.OSMTagIdToKeyValue[routeType]);
                            else
                                routeTagS      = String.Format("{0,30}","Not available");
                            routeTransportModesS = String.Format("{0,30}",TransportModes.MaskToString(TransportModes.TagIdToTransportModes(routeType)));
                            timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[n].M)); // <- debug: check units
                            vertexS      = String.Format("{0,8}", n+1);
                            nodeS        = String.Format("{0,20}", node.OsmID);
                            timeStampS   = String.Format("{0,14}", timeStamp);
                            coordinateXS = String.Format("{0,20}", routeCoordinates[n].X);
                            coordinateYS = String.Format("{0,20}", routeCoordinates[n].Y);
                            nodeIdxS     = String.Format("{0,14}", node.Idx);
                            logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                            transportModeRepetitions=0;
                        }
                        else
                        {
                            if(transportModeRepetitions<1)
                                logger.Debug("{0,10}\t{1,20}\t{2,20}\t{3,14}\t{4,18}\t{5,10}\t{6,30}\t{7,30}\t{8,14}\t{9,20}","|  ","|","|","|","| ","|","|","| ","|","|");
                            transportModeRepetitions++;
                        }
                    }
                    node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[route.Count -1].X, routeCoordinates[route.Count -1].Y);
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M)); // <- debug: check units
                    vertexS        = String.Format("{0,8}", routeCoordinates.Length);
                    nodeS          = String.Format("{0,20}", node.OsmID);
                    timeStampS     = String.Format("{0,14}", timeStamp);
                    coordinateXS   = String.Format("{0,20}", routeCoordinates[route.Count -1].X);
                    coordinateYS   = String.Format("{0,20}", routeCoordinates[route.Count -1].Y);
                    if(transportModeTransitions.ContainsKey(node.Idx))
                    {
                        transportModeS = String.Format("{0,18}",TransportModes.MaskToString(transportModeTransitions[node.Idx].Item1));
                        var routeType = transportModeTransitions[node.Idx].Item2;
                        routeTypeS     = String.Format("{0,10}",routeType);
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                            routeTagS      = String.Format("{0,30}",TransportModes.OSMTagIdToKeyValue[routeType]);
                        else
                            routeTagS      = String.Format("{0,30}","Not available");
                        routeTransportModesS = String.Format("{0,30}",TransportModes.MaskToString(TransportModes.TagIdToTransportModes(routeType)));
                    }
                    else
                    {
                        transportModeS = String.Format("{0,18}","| ");
                        routeTypeS     = String.Format("{0,10}","|");
                        routeTagS      = String.Format("{0,30}","|");
                        routeTransportModesS     = String.Format("{0,30}","| ");
                    }
                    nodeIdxS       = String.Format("{0,14}", node.Idx);
                    logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                }
                catch (Exception e)
                {
                    logger.Debug("Unable to display data:", e.Message);
                }
            }
        }

        public static void TraceRouteDetails(LineString route, Tuple<string[],DateTime[]> TTextTransitions)
        {
            var routeCoordinates = route.Coordinates;

            Node node;
            string timeStamp;

            if(TTextTransitions!=null)
            {
                var transportModes=TTextTransitions.Item1;
                var timeStamps=TTextTransitions.Item2;

                try
                {
                    logger.Debug("Transport mode transitions:");
                    logger.Debug("Index\tTransport mode\tTime stamp");
                    for(int i=0; i<transportModes.Length; ++i)
                    {
                       logger.Debug("{0}\t{1}\t{2}",i,transportModes[i],timeStamps[i]);
                    }

                    logger.Debug("> Route ({0} vertices)", routeCoordinates.Length);
                    if(routeCoordinates.Length<1)
                    {
                        logger.Debug("> Empty route");
                        return;
                    }
                    string vertexS        = String.Format("{0,8}","Vertex");
                    string nodeS          = String.Format("{0,20}","Node OSM Id");
                    string timeStampS     = String.Format("{0,14}","Time stamp");
                    string coordinateXS   = String.Format("{0,20}","Coordinate X");
                    string coordinateYS   = String.Format("{0,20}","Coordinate Y");
                    string transportModeS = String.Format("{0,18}","Transport Mode");
                    string routeTypeS     = String.Format("{0,10}","Route Type");
                    string routeTagS      = String.Format("{0,30}","Route Tag (Value : Key)");
                    string routeTransportModesS      = String.Format(" {0,30}","Route Allowed Transport Modes");
                    string nodeIdxS       = String.Format(" {0,14}","Node Idx");
                    logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                    logger.Debug("=======================================================================================================================================================================================================================");
                    
                    byte currentTransportMode = 0;
                    byte previousTransportMode = 0;
                    for(var n = 0; n < routeCoordinates.Length-1; n++)
                    {
                        node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);

                            previousTransportMode = currentTransportMode;    
                            transportModeS = String.Format("{0,18}",TransportModes.MaskToString(currentTransportMode));

                            var routeType = 0;
                            routeTypeS     = String.Format("{0,10}",routeType);
                            if(TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                                routeTagS      = String.Format("{0,30}",TransportModes.OSMTagIdToKeyValue[routeType]);
                            else
                                routeTagS      = String.Format("{0,30}","Not available");

                            routeTransportModesS = String.Format("{0,30}",TransportModes.MaskToString(node.GetAvailableOutboundTransportModes()));
                            timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[n].M)); // <- debug: check units
                            vertexS      = String.Format("{0,8}", n+1);
                            nodeS        = String.Format("{0,20}", node.OsmID);
                            timeStampS   = String.Format("{0,14}", timeStamp);
                            coordinateXS = String.Format("{0,20}", routeCoordinates[n].X);
                            coordinateYS = String.Format("{0,20}", routeCoordinates[n].Y);
                            nodeIdxS     = String.Format("{0,14}", node.Idx);
                            logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                    }
                    node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[route.Count -1].X, routeCoordinates[route.Count -1].Y);
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M)); // <- debug: check units
                    vertexS        = String.Format("{0,8}", routeCoordinates.Length);
                    nodeS          = String.Format("{0,20}", node.OsmID);
                    timeStampS     = String.Format("{0,14}", timeStamp);
                    coordinateXS   = String.Format("{0,20}", routeCoordinates[route.Count -1].X);
                    coordinateYS   = String.Format("{0,20}", routeCoordinates[route.Count -1].Y);
                    nodeIdxS       = String.Format("{0,14}", node.Idx);
                    logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                }
                catch (Exception e)
                {
                    logger.Debug("Unable to display data:", e.Message);
                }
            }
        }

        // public virtual Task<TimeSpan> Run<A,D,U,R>(Graph graph, string connectionString, string routeTable, string comparisonTable, int numberOfRuns) where A: IRoutingAlgorithm, new() where D: IPersonaDownloader, new() where U: IRouteUploader, new() where R: IRouter, new()
        // {
        //     throw new NotImplementedException();
        // }
    }
}