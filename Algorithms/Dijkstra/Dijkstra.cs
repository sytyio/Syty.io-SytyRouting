using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.Dijkstra
{
    public class Dijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();

        public void TraceRoute()
        {
            logger.Debug("Displaying {0} route Nodes (OsmId):", route.Count);
            foreach(Node node in route)
            {
                logger.Debug("{0}", node.OsmID);
            }
        }

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode, byte[] transportModesSequence)
        {
            route.Clear();
            transportModeTransitions.Clear();
            routeCost = 0;

            //DEBUG:
            if(originNode.OsmID==9118117)
            {
                Console.WriteLine("Problem");
            }


            var transportModeQueue = new Queue<byte>(transportModesSequence.Length);
            for(int i = 0; i < transportModesSequence.Length; i++)
            {
                transportModeQueue.Enqueue(transportModesSequence[i]);
            }

            
            if(transportModeQueue.TryDequeue(out byte initialTransportMode))
            {
                AddStep(null, originNode, 0, initialTransportMode);
            }
            else
            {
                logger.Debug("Error retrieving initial Transport Mode from queue.");
            }
            

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;

                byte currentTransportMode = currentStep.TransportMode;

                if(activeNode == destinationNode)
                {
                    ReconstructRoute(currentStep);
                    routeCost = currentStep.CumulatedCost;
                    
                    break;
                }
                if(priority <= bestScoreForNode[activeNode!.Idx])
                {
                    foreach(var outwardEdge in activeNode.OutwardEdges)
                    {
                        if((outwardEdge.TransportModes & currentTransportMode) == currentTransportMode)
                        {
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, currentTransportMode);
                            //if((transportModeQueue.TryPeek(out byte nextTransportMode) && (outwardEdge.TransportModes & nextTransportMode) == nextTransportMode))
                            //    AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, nextTransportMode);
                        }
                        if((transportModeQueue.TryDequeue(out byte nextTransportMode) && (outwardEdge.TransportModes & nextTransportMode) == nextTransportMode))
                        {
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, nextTransportMode);
                        }
                    }
                }
            }

            dijkstraStepsQueue.Clear();
            bestScoreForNode.Clear();

            return route;
        }

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, byte transportMode)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, TransportMode = transportMode };
                dijkstraStepsQueue.Enqueue(step, cumulatedCost);

                if(!exist)
                {
                    bestScoreForNode.Add(nextNode.Idx, cumulatedCost);
                }
                else
                {
                    bestScoreForNode[nextNode.Idx] = cumulatedCost;
                }
            }
        }

        private void ReconstructRoute(DijkstraStep? currentStep)
        {
            if (currentStep != null)
            {
                if(currentStep.PreviousStep != null)
                {
                    if(currentStep.PreviousStep.TransportMode != currentStep.TransportMode && !transportModeTransitions.ContainsKey(currentStep.ActiveNode.Idx))
                        transportModeTransitions.Add(currentStep.ActiveNode.Idx, currentStep.TransportMode);
                }
                else
                {
                    transportModeTransitions.Add(currentStep.ActiveNode.Idx, currentStep.TransportMode);
                }
                ReconstructRoute(currentStep.PreviousStep);
                route.Add(currentStep.ActiveNode!);
            }
        }
    }
}