using System.Diagnostics;
using NLog;

namespace SytyRouting
{
    public class Dijkstra
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Node[] nodes =  new Node[0];

        public Dijkstra(Node[] graphNodes)
        {
            nodes = graphNodes;
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            
            var route = new List<Node>();

            Dictionary<int, Node> visitedNodes = new Dictionary<int, Node>();

            PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
            PriorityQueue<DijkstraStep, double> backwardStartSteps = new PriorityQueue<DijkstraStep, double>();

            var originNode = Array.Find(nodes, n => n.OsmID == originNodeOsmId);
            var destinationNode = Array.Find(nodes, n => n.OsmID == destinationNodeOsmId);
            if(originNode == null)
            {
                logger.Info("Origin node (source_osm = {0}) not found", originNodeOsmId);
            }
            else if(destinationNode == null)
            {
                logger.Info("Destination node (source_osm = {0}) not found", destinationNodeOsmId);
            }
            else
            {
                logger.Info("Origin Node     \t OsmId = {0}", originNode?.OsmID);
                logger.Info("Destination Node\t OsmId = {0}", destinationNode?.OsmID);

                var firstStep = new DijkstraStep{TargetNode = originNode, CumulatedCost = 0};
                dijkstraStepsQueue.Enqueue(firstStep, firstStep.CumulatedCost);
                
                while(!visitedNodes.ContainsKey(destinationNode!.Idx))
                {
                    dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority);
                    var targetNode = currentStep!.TargetNode;
                
                    if(targetNode != null && !visitedNodes.ContainsKey(targetNode.Idx))
                    {
                        foreach(var outwardEdge in targetNode.OutwardEdges)
                        {
                            if(!visitedNodes.ContainsKey(outwardEdge.TargetNode!.Idx))
                            {
                                var dijkstraStep = new DijkstraStep{PreviousStep = currentStep, TargetNode = outwardEdge.TargetNode, CumulatedCost = outwardEdge.Cost + currentStep.CumulatedCost};
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