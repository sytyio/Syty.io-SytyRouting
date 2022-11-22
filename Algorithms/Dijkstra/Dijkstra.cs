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
                byte[] transportModes = TransportModes.MaskToArray(transportModesSequence[0]);
                for(int i=0; i<transportModes.Length; i++)
                {
                    AddStep(null, originNode, 0, 0, transportModes[i]);
                }
            }
            else
            {
                foreach(var outwardEdge in originNode.OutwardEdges)
                {
                    byte[] availableTransportModes = TransportModes.MaskToArray(outwardEdge.TransportModes);
                    for(int i = 0; i < availableTransportModes.Length; i++)
                    {
                        AddStep(null, originNode, 0, -1, availableTransportModes[i]);
                    }
                }
            }

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;

                int currentTransportPlaceholder = currentStep.TransportModePlaceholder;
                byte currentTransportMask;
                if(TrasnportModeSequence.Length>0)
                {
                    currentTransportMask = transportModesSequence[currentTransportPlaceholder];
                }
                else
                {
                    currentTransportMask = currentStep.TransportMode;
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
                        var edgeTransportModes = outwardEdge.TransportModes;

                        if(transportModesSequence.Length>0)
                        {
                            var currentMaskArray = TransportModes.MaskToArray(currentTransportMask);
                            for(int i=0; i<currentMaskArray.Length; i++)
                            {
                                var transportMode = currentMaskArray[i];
                                
                                if((edgeTransportModes & transportMode) == transportMode)
                                {
                                    AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, currentTransportPlaceholder, transportMode);
                                }

                                if(currentTransportPlaceholder>=0 && currentTransportPlaceholder<transportModesSequence.Length-1)
                                {
                                    byte nextTransportMode = transportModesSequence[currentTransportPlaceholder+1];
                                    if((edgeTransportModes & nextTransportMode) == nextTransportMode)
                                    {
                                        AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, currentTransportPlaceholder+1, nextTransportMode);
                                    }
                                }
                            }
                        }
                        else
                        {           
                            var key = TransportModes.RoutingRulesContainKey(currentTransportMask);         

                            if(key!=0)
                            {
                                byte[] alternativeTransportModes = TransportModes.MaskToArray(TransportModes.RoutingRules[key]);
                                for(int i = 0; i < alternativeTransportModes.Length; i++)
                                {
                                    var transportMode = alternativeTransportModes[i];
                                    if((edgeTransportModes & transportMode) == transportMode)
                                    {
                                        AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, -1, transportMode);
                                    }
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

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, int transportModePlaceholder, byte transportMode)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, TransportModePlaceholder = transportModePlaceholder, TransportMode = transportMode };
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
                    {
                        AddTransportModeTransition(currentStep.TransportMode, currentStep.ActiveNode.Idx);
                    }
                }
                else
                {
                    AddTransportModeTransition(currentStep.TransportMode, currentStep.ActiveNode.Idx);
                }
                ReconstructRoute(currentStep.PreviousStep);
                route.Add(currentStep.ActiveNode!);
            }
        }

        private void AddTransportModeTransition(byte transportMode, int nodeIdx)
        {
            transportModeTransitions.Add(nodeIdx, transportMode);
        }
    }
}