using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.Dijkstra
{
    public class Dijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();

        byte[] TrasnportModeSequence = null!;

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode, byte[] transportModesSequence)
        {
            route.Clear();
            transportModeTransitions.Clear();
            routeCost = 0;
            TrasnportModeSequence = transportModesSequence;

            //DEBUG:
            if(originNode.OsmID==2595939392)
            {
                Console.WriteLine("Problemo");
            }
            
            AddStep(null, originNode, 0, 0);

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;

                int currentTransportModeIndex = currentStep.TransportModeSequenceIndex;
                byte currentTransportMode = transportModesSequence[currentTransportModeIndex];
                

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
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, currentTransportModeIndex);
                        }

                        byte nextTransportMode = transportModesSequence[currentTransportModeIndex+1];
                        if(currentStep.TransportModeSequenceIndex < transportModesSequence.Length && (outwardEdge.TransportModes & nextTransportMode) == nextTransportMode)
                        {
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, currentTransportModeIndex+1);
                        }
                    }
                }
            }

            dijkstraStepsQueue.Clear();
            bestScoreForNode.Clear();

            return route;
        }

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, int transportModeIndex)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, TransportModeSequenceIndex = transportModeIndex };
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
                    if(currentStep.PreviousStep.TransportModeSequenceIndex != currentStep.TransportModeSequenceIndex && !transportModeTransitions.ContainsKey(currentStep.ActiveNode.Idx))
                        transportModeTransitions.Add(currentStep.ActiveNode.Idx, TrasnportModeSequence[currentStep.TransportModeSequenceIndex]);
                }
                else
                {
                    transportModeTransitions.Add(currentStep.ActiveNode.Idx, TrasnportModeSequence[currentStep.TransportModeSequenceIndex]);
                }
                ReconstructRoute(currentStep.PreviousStep);
                route.Add(currentStep.ActiveNode!);
            }
        }
    }
}