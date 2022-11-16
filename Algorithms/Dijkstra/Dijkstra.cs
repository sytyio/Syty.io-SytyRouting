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
            
            if(transportModesSequence.Length>0)
            {
                AddStep(null, originNode, 0, 0);
            }
            else
            {
                foreach(var outwardEdge in originNode.OutwardEdges)
                {
                    byte[] availableTransportModes = TransportModes.MaskToArray(outwardEdge.TransportModes);
                    for(int i = 0; i < availableTransportModes.Length; i++)
                    {
                        AddStep(null, originNode, 0, -1*availableTransportModes[i]);
                    }
                }
            }

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;

                int currentTransportModeIndex = currentStep.TransportModeSequenceIndex;
                byte currentTransportMode;
                if(TrasnportModeSequence.Length>0)
                {
                    currentTransportMode = transportModesSequence[currentTransportModeIndex];
                }
                else
                {
                    currentTransportMode = (byte)(-1*currentTransportModeIndex);
                }
                
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

                        if(currentTransportModeIndex>0 && currentTransportModeIndex<transportModesSequence.Length-1)
                        {
                            byte nextTransportMode = transportModesSequence[currentTransportModeIndex+1];
                            if((outwardEdge.TransportModes & nextTransportMode) == nextTransportMode)
                            {
                                AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, currentTransportModeIndex+1);
                            }
                        }
                        else
                        {                    
                            if(TransportModes.RoutingRules.ContainsKey(currentTransportMode))
                            {
                                byte[] alternativeTransportModes = TransportModes.MaskToArray(TransportModes.RoutingRules[currentTransportMode]);
                                for(int i = 0; i < alternativeTransportModes.Length; i++)
                                {
                                    AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, -1*alternativeTransportModes[i]);
                                }
                            }                    
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
                    {
                        AddTransportModeTransition(currentStep.TransportModeSequenceIndex, currentStep.ActiveNode.Idx);
                    }
                }
                else
                {
                    AddTransportModeTransition(currentStep.TransportModeSequenceIndex, currentStep.ActiveNode.Idx);
                }
                ReconstructRoute(currentStep.PreviousStep);
                route.Add(currentStep.ActiveNode!);
            }
        }

        private void AddTransportModeTransition(int currentStepTMIndex, int nodeIdx)
        {
            if(currentStepTMIndex>0)
            {
                transportModeTransitions.Add(nodeIdx, TrasnportModeSequence[currentStepTMIndex]);
            }
            else
            {
                transportModeTransitions.Add(nodeIdx, (byte)(-1*currentStepTMIndex));
            }
        }
    }
}