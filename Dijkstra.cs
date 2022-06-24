using System.Diagnostics;
using NLog;

namespace SytyRouting
{
    public class Dijkstra
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Graph _graph;

        public Dijkstra(Graph graph)
        {
            _graph = graph;
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            
            var route = new List<Node>();

            Node originNode;
            Node destinationNode;

            Dictionary<int, Node> visitedNodes = new Dictionary<int, Node>();

            List<DijkstraStep> dijkstraSteps = new List<DijkstraStep>();
            PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
            PriorityQueue<DijkstraStep, double> backwardStartSteps = new PriorityQueue<DijkstraStep, double>();

            try
            {
                originNode = _graph.GetNodeByOsmId(originNodeOsmId);
            }
            catch (ArgumentException e)
            {
                logger.Info("Origin node (source_osm = {0}) not found", originNodeOsmId);
                logger.Info("{0}: {1}", e.GetType().Name, e.Message);

                return route;
            }

            try
            {
                destinationNode = _graph.GetNodeByOsmId(destinationNodeOsmId);
            }
            catch (ArgumentException e)
            {
                logger.Info("Destination node (source_osm = {0}) not found", destinationNodeOsmId);
                logger.Info("{0}: {1}", e.GetType().Name, e.Message);

                return route;
            }

            logger.Info("Origin Node     \t OsmId = {0}", originNode?.OsmID);
            logger.Info("Destination Node\t OsmId = {0}", destinationNode?.OsmID);

            var firstStep = new DijkstraStep{TargetNode = originNode, CumulatedCost = 0};
            dijkstraSteps.Add(firstStep);

            firstStep.Id = dijkstraSteps.FindIndex(s => Equals(s, firstStep));

            dijkstraStepsQueue.Enqueue(firstStep, firstStep.CumulatedCost);

            var skipStep = false;
            
            while(!visitedNodes.ContainsKey(destinationNode!.Idx))
            {
                dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority);
                dijkstraSteps.Remove(currentStep!);
                
                var targetNode = currentStep!.TargetNode;

                // check for previously found steps with the same Target Node and lower cumulated cost
                foreach(var step in dijkstraSteps)
                {
                    if(step.TargetNode!.Idx == targetNode!.Idx && step.CumulatedCost < currentStep.CumulatedCost)
                    {
                        skipStep = true;
                        break;
                    }
                }

                if(skipStep)
                {
                    skipStep = false;
                    continue;
                }
                
                if(targetNode != null && !visitedNodes.ContainsKey(targetNode.Idx))
                {
                    foreach(var outwardEdge in targetNode.OutwardEdges)
                    {
                        if(!visitedNodes.ContainsKey(outwardEdge.TargetNode!.Idx))
                        {
                            var dijkstraStep = new DijkstraStep{PreviousStep = currentStep, TargetNode = outwardEdge.TargetNode, CumulatedCost = outwardEdge.Cost + currentStep.CumulatedCost};
                            dijkstraSteps.Add(dijkstraStep);
                            dijkstraStep.Id = dijkstraSteps.FindIndex(s => Equals(s, dijkstraStep));

                            dijkstraStepsQueue.Enqueue(dijkstraStep, dijkstraStep.CumulatedCost);
                            
                            if(dijkstraStep.TargetNode.OsmID == destinationNodeOsmId)
                            {
                                backwardStartSteps.Enqueue(dijkstraStep, dijkstraStep.CumulatedCost);
                            }
                        }
                    }
                    visitedNodes.Add(targetNode.Idx, targetNode);
                }
            }

            backwardStartSteps.TryPeek(out DijkstraStep? firstBackwardStep, out double totalCost);

            route.Add(firstBackwardStep!.TargetNode!);

            logger.Info("Route reconstruction:");
            var currentBackwardStep = firstBackwardStep;
            
            while(currentBackwardStep.TargetNode?.OsmID != originNodeOsmId)
            {
                var nextBackwardStep = currentBackwardStep.PreviousStep;
                route.Add(nextBackwardStep!.TargetNode!);
                currentBackwardStep = nextBackwardStep;
            }

            route.Reverse();

            foreach(var node in route)
            {
                logger.Info("Node OsmId = {0}", node.OsmID);
            }

            stopWatch.Stop();
            var totalTime = FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Route created in {0} (HH:MM:S.mS)", totalTime);

            return route;
        }

        private string FormatElapsedTime(TimeSpan timeSpan)
        {
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
                timeSpan.Milliseconds);

            return elapsedTime;
        }
    }
}