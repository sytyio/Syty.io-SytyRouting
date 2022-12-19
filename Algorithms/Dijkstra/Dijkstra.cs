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
                byte transportMode = transportModesSequence[0];
                AddStep(null, originNode, 0, 0, transportMode);
            }
            else
            {
                foreach(var outwardEdge in originNode.OutwardEdges)
                {
                    var availableTransportModes = TransportModes.MaskToList(outwardEdge.TransportModes);
                    foreach(var transportMode in availableTransportModes)
                    {
                        AddStep(null, originNode, 0, -1, transportMode);
                    }
                }
            }

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;

                int currentTransportIndex = currentStep.TransportSequenceIndex;
                byte currentTransportMask;
                if(TrasnportModeSequence.Length>0)
                {
                    currentTransportMask = transportModesSequence[currentTransportIndex];
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
                            var transportMode = currentTransportMask;
                         
                            if((edgeTransportModes & transportMode) == transportMode)
                            {
                                var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, transportMode);
                                AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex, transportMode);
                            }

                            if((transportMode & TransportModes.PublicModes) != 0 && (edgeTransportModes & TransportModes.DefaultMode) == TransportModes.DefaultMode)
                            {
                                var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, TransportModes.DefaultMode);
                                AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex, TransportModes.DefaultMode);
                            }

                            if(currentTransportIndex>=0 && currentTransportIndex<transportModesSequence.Length-1)
                            {
                                byte nextTransportMode = transportModesSequence[currentTransportIndex+1];

                                if((edgeTransportModes & nextTransportMode) == nextTransportMode)
                                {
                                    var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, nextTransportMode);
                                    AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex+1, nextTransportMode);
                                }
                            }
                        }
                        else
                        {           
                            var key = TransportModes.RoutingRulesContainKey(currentTransportMask);         

                            if(key!=0)
                            {
                                var alternativeTransportModes = TransportModes.MaskToList(TransportModes.RoutingRules[key]);
                                foreach(var transportMode in alternativeTransportModes)
                                {
                                    if((edgeTransportModes & transportMode) == transportMode)
                                    {
                                        var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, transportMode);
                                        AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, -1, transportMode);
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

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, int transportSequenceIndex, byte transportMode)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, TransportSequenceIndex = transportSequenceIndex, TransportMode = transportMode };
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
                        transportModeTransitions.Add(currentStep.ActiveNode.Idx, currentStep.TransportMode);
                    }
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