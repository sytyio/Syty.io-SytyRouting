using System.Diagnostics.CodeAnalysis;
using NLog;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using SytyRouting.Algorithms;
using SytyRouting.DataBase;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace SytyRouting.Routing
{
    public abstract class BaseRouter : IRouter
    {
        [NotNull]
        protected Graph? _graph = null!;
        protected string _routeTable = null!;
        protected string _auxiliaryTable = null!;
        protected List<Persona> Personas = null!;
        protected int ComputedRoutesCount = 0;
        protected TimeSpan TotalRoutingTime = TimeSpan.Zero;
        protected TimeSpan TotalUploadingTime = TimeSpan.Zero;

        protected int sequenceValidationErrors = 0;

        protected Stopwatch baseRouterStopWatch = new Stopwatch();
        protected int elementsToProcess = 0;
        protected int computedRoutes = 0;
        protected int processedDbElements = 0;
        protected int uploadedRoutes = 0;
        protected List<Persona> personas = new List<Persona>();

        protected bool routingTasksHaveEnded = false;


        protected static int simultaneousRoutingTasks = Environment.ProcessorCount;
        protected Task[] routingTasks = new Task[simultaneousRoutingTasks];
        protected int taskArraysQueueThreshold = simultaneousRoutingTasks;

        protected ConcurrentQueue<Persona[]> personaTaskArraysQueue = new ConcurrentQueue<Persona[]>();
        protected int regularBatchSize = simultaneousRoutingTasks * Configuration.RegularRoutingTaskBatchSize;


        protected int originEqualsDestinationErrors = 0;


        private static Logger logger = LogManager.GetCurrentClassLogger();


        public void Initialize(Graph graph, string routeTable, string auxiliaryTable)
        {
            _graph = graph;
            _routeTable = routeTable;
            _auxiliaryTable = auxiliaryTable;
        }

        public List<Persona> GetPersonas()
        {
            return Personas;
        }

        public int GetComputedRoutesCount()
        {
            return ComputedRoutesCount;
        }

        public TimeSpan GetRoutingTime()
        {
            return TotalRoutingTime;
        }

        public TimeSpan GetUploadingTime()
        {
            return TotalUploadingTime;
        }

        protected byte[] ValidateTransportSequence(int id, Point homeLocation, Point workLocation, string[] transportSequence)
        {
            byte[] formattedSequence = TransportModes.CreateSequence(transportSequence);

            byte initialTransportMode = formattedSequence.First();
            byte finalTransportMode = formattedSequence.Last();

            var originNode = _graph.GetNodeByLongitudeLatitude(homeLocation.X, homeLocation.Y, isSource: true);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(workLocation.X, workLocation.Y, isTarget: true);

            Edge outboundEdge = originNode.GetFirstOutboundEdge(initialTransportMode);
            Edge inboundEdge = destinationNode.GetFirstInboundEdge(finalTransportMode);

            if(outboundEdge != null && inboundEdge != null)
            {
                return formattedSequence;
            }
            else
            {
                sequenceValidationErrors++;

                logger.Debug("!===================================!");
                logger.Debug(" Transport sequence validation error:");
                logger.Debug("!===================================!");
                logger.Debug(" Persona Id: {0}", id);
                if(outboundEdge == null)
                {
                    logger.Debug(" ORIGIN Node Idx {0} does not contain the requested transport mode '{1}'.",originNode.Idx,TransportModes.SingleMaskToString(initialTransportMode)); // Use MaskToString() if the first byte in the sequence represents more than one Transport Mode

                    var outboundEdgeTypes = originNode.GetOutboundEdgeTypes();
                    string outboundEdgeTypesS = "";
                    foreach(var edgeType in outboundEdgeTypes)
                    {
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(edgeType))
                            outboundEdgeTypesS += edgeType.ToString() +  " " + TransportModes.OSMTagIdToKeyValue[edgeType] + " ";
                    }
                    logger.Debug(" Outbound Edge type(s): {0}",outboundEdgeTypesS);
                    
                    var outboundTransportModes = originNode.GetAvailableOutboundTransportModes();
                    logger.Debug(" Available outbound transport modes at origin: {0}",TransportModes.MaskToString(outboundTransportModes));
                    initialTransportMode = TransportModes.MaskToArray(outboundTransportModes).First();
                }

                if(inboundEdge == null)
                {
                    logger.Debug(" DESTINATION Node Idx {0} does not contain the requested transport mode '{1}'.",destinationNode.Idx,TransportModes.SingleMaskToString(finalTransportMode));

                    var inboundEdgeTypes = destinationNode.GetInboundEdgeTypes();
                    string inboundEdgeTypesS = "";
                    foreach(var edgeType in inboundEdgeTypes)
                    {
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(edgeType))
                            inboundEdgeTypesS += edgeType.ToString() +  " " + TransportModes.OSMTagIdToKeyValue[edgeType] + " ";
                    }
                    logger.Debug(" Inbound Edge type(s): {0}",inboundEdgeTypesS);
                    
                    var inboundTransportModes = destinationNode.GetAvailableInboundTransportModes();
                    logger.Debug(" Available inbound transport modes at destination: {0}",TransportModes.MaskToString(inboundTransportModes));
                    finalTransportMode = TransportModes.MaskToArray(inboundTransportModes).First();
                }
            
                var newSequence = new byte[2];
                newSequence[0] = initialTransportMode;
                newSequence[1] = finalTransportMode;
                logger.Debug(" Requested transport sequence overridden to: {0}", TransportModes.ArrayToNames(newSequence));
                
                return newSequence;
            }
        }

        protected bool CalculateRoute(IRoutingAlgorithm routingAlgorithm, ref Persona persona)
        {
            var homeX = persona.HomeLocation!.X;
            var homeY = persona.HomeLocation.Y;
            
            var workX = persona.WorkLocation!.X;
            var workY = persona.WorkLocation.Y;
            
            var requestedTransportModes = persona.RequestedTransportSequence;

            TimeSpan initialTime = TimeSpan.Zero;

            List<Node> route = null!;

            var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y, isSource: true);
            var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y, isTarget: true);

            if(origin == destination)
            {
                logger.Debug("Origin and destination nodes are equal for Persona Id {0}", persona.Id);

                persona.Route = routingAlgorithm.TwoPointLineString(homeX, homeY, workX, workY, TransportModes.DefaultMode, initialTime);

                if(persona.Route.IsEmpty)
                {
                    logger.Debug("Route is empty for Persona Id {0} !!!!", persona.Id);
                    
                    return false;
                }

                persona.TTextTransitions = routingAlgorithm.SingleTransportModeTransition(persona, origin, destination, TransportModes.DefaultMode);

                persona.SuccessfulRouteComputation = true;

                return true;
            }
            else
            {
                route = routingAlgorithm.GetRoute(origin, destination, requestedTransportModes);
            }

            persona.Route = routingAlgorithm.NodeRouteToLineStringMSeconds(homeX, homeY, workX, workY, route, initialTime, persona.StartDateTime);

            persona.TTextTransitions = routingAlgorithm.GetTransportModeTransitions();
            
            //if(route != null)
            if(!persona.Route.IsEmpty)
            {
                //if(route.Count > 0)
                //{
                    // persona.Route = routingAlgorithm.NodeRouteToLineStringMSeconds(homeX, homeY, workX, workY, route, initialTime, persona.StartDateTime);

                    // persona.TTextTransitions = routingAlgorithm.GetTransportModeTransitions();

                    persona.SuccessfulRouteComputation = true;

                    return true;
                //}
            }
            else
            {
                logger.Debug("Route is empty for Persona Id {0}", persona.Id);
            }
            //}

            return false;
        }

        protected void MonitorRouteCalculation()
        {
            int monitorSleepMilliseconds = Configuration.MonitorSleepMilliseconds; // 5_000;
            while(true)
            {
                var timeSpan = baseRouterStopWatch.Elapsed;
                var timeSpanMilliseconds = baseRouterStopWatch.ElapsedMilliseconds;
                Helper.DataLoadBenchmark(elementsToProcess, computedRoutes, timeSpan, timeSpanMilliseconds, logger);
                logger.Info("DB elements already processed: {0} ({1:0.000} %). Computed routes: {2} ({3:0.000} %)", processedDbElements, (double)processedDbElements / elementsToProcess * 100, computedRoutes, (double)computedRoutes / elementsToProcess * 100);
                logger.Info("");

                if(routingTasksHaveEnded)
                {
                    if(processedDbElements != elementsToProcess)
                    {
                        logger.Info(" ==>> Inconsistent number of processed elements.");
                    }
                    logger.Debug("{0} routes (out of {1}) uploaded ({2} %)", uploadedRoutes, personas.Count, 100 * uploadedRoutes / personas.Count);
                    return;
                }

                Thread.Sleep(monitorSleepMilliseconds);
            }
        }

        protected int[] GetBatchPartition(int regularSlice, int whole, int numberOfSlices)
        {
            int lastSlice = whole - regularSlice * (numberOfSlices - 1);
            int[] batchPartition = new int[numberOfSlices];
            for (var i = 0; i < batchPartition.Length-1; i++)
            {
                batchPartition[i] = regularSlice;
            }
            batchPartition[batchPartition.Length-1] = lastSlice;

            return batchPartition;
        }

        public virtual Task StartRouting<A,U>() where A: IRoutingAlgorithm, new() where U: IRouteUploader, new()
        {
            throw new NotImplementedException();
        }

        protected virtual void CalculateRoutes<A,U>(int taskIndex) where A: IRoutingAlgorithm, new() where U: IRouteUploader, new()
        {
            throw new NotImplementedException();
        }

        protected virtual Task UploadRoutesAsync<U>() where U: IRouteUploader, new()
        {
            throw new NotImplementedException();
        }
    }
}