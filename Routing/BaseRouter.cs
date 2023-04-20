using System.Diagnostics.CodeAnalysis;
using NLog;
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
        protected string _connectionString = null!;
        protected string _routeTable = null!;
        protected string _comparisonTable = null!;
        protected string _benchmarkTable = null!;
        protected List<Persona> Personas = new List<Persona>();
        protected int ComputedRoutesCount = 0;
        protected TimeSpan TotalRoutingTime = TimeSpan.Zero;
        protected TimeSpan TotalUploadingTime = TimeSpan.Zero;
        protected TimeSpan TotalDownloadingTime = TimeSpan.Zero;

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


        public void Initialize(Graph graph, string connectionString, string routeTable, string comparisonTable = "", string benchmarkTable = "")
        {
            _graph = graph;
            _connectionString = connectionString;
            _routeTable = routeTable;
            _comparisonTable = comparisonTable;
            _benchmarkTable = benchmarkTable;
        }

        public void Reset()
        {
            personas.Clear();
            Personas.Clear();

            ComputedRoutesCount = 0;
            TotalRoutingTime = TimeSpan.Zero;
            TotalUploadingTime = TimeSpan.Zero;
            TotalDownloadingTime = TimeSpan.Zero;

            baseRouterStopWatch.Reset();
            elementsToProcess = 0;
            computedRoutes = 0;
            processedDbElements = 0;
            uploadedRoutes = 0;
            routingTasksHaveEnded = false;
            personaTaskArraysQueue.Clear();
            originEqualsDestinationErrors = 0;
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

        public TimeSpan GetDownloadingTime()
        {
            return TotalDownloadingTime;
        }

        public TimeSpan GetUploadingTime()
        {
            return TotalUploadingTime;
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
            
            if(!persona.Route.IsEmpty)
            {
                    persona.SuccessfulRouteComputation = true;

                    return true;
            }
            else
            {
                logger.Debug("Route is empty for Persona Id {0}", persona.Id);
            }

            return false;
        }

        protected void MonitorRouteCalculation()
        {
            int monitorSleepMilliseconds = Configuration.MonitorSleepMilliseconds; // 5_000;
            while(true)
            {
                var timeSpan = baseRouterStopWatch.Elapsed;
                Helper.DataLoadBenchmark(elementsToProcess, computedRoutes, timeSpan, logger);
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

        protected virtual Task DownloadPersonasAsync<D>() where D: IPersonaDownloader, new()
        {
            throw new NotImplementedException();
        }

        public virtual Task StartRouting<A,D,U>() where A: IRoutingAlgorithm, new() where D: IPersonaDownloader, new() where U: IRouteUploader, new()
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