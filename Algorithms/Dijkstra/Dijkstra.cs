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
            var sequenceLength = transportModesSequence.Length;
           
            if(sequenceLength>0)
            {
                byte outboundMode  = transportModesSequence.First(); // requested mode
                Edge outboundEdge = originNode.GetFirstOutboundEdge(outboundMode);
                if(outboundEdge != null)
                {
                    byte inboundMode = TransportModes.None; // inbound transport mode (wrt the step active node)
                    var inboundRouteType = TransportModes.NoRouteType;
                    AddStep(null, originNode, 0, 0, inboundMode, inboundRouteType);
                }
            }

            Node activeNode = new Node();

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                activeNode = currentStep!.ActiveNode;

                int transportIndex = currentStep.TransportSequenceIndex;
                byte inboundMode = currentStep.InboundTransportMode;
                byte outboundMode = transportModesSequence[transportIndex];
                             
                if(activeNode == destinationNode)
                {
                    byte outboundTransportMode=TransportModes.None;
                    int outboundRouteType=TransportModes.NoRouteType;
                    ReconstructRoute(currentStep,outboundTransportMode,outboundRouteType);
                    routeCost = currentStep.CumulatedCost;
                    
                    break;
                }

                if(priority <= bestScoreForNode[activeNode!.Idx])
                {
                    foreach(var outwardEdge in activeNode.OutwardEdges)
                    {
                        var availableOutboundModes = outwardEdge.TransportModes;
                        var currentMode = inboundMode;
                        
                        if((availableOutboundModes & outboundMode) == outboundMode)
                        {
                            var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, outboundMode);
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, transportIndex, outboundMode, outwardEdge.TagIdRouteType);
                        }

                        if(transportIndex<sequenceLength-1)
                        {
                            byte nextTransportMode = transportModesSequence[transportIndex+1];

                            if((availableOutboundModes & nextTransportMode) == nextTransportMode)
                            {
                                var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, nextTransportMode);
                                AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, transportIndex+1, nextTransportMode, outwardEdge.TagIdRouteType);
                            }
                        }
                    }
                }
            }

            if(activeNode!=destinationNode)
            {
                logger.Debug("!############################!");
                logger.Debug(" Destination node not reached!");
                logger.Debug("!############################!");
            }

            dijkstraStepsQueue.Clear();
            bestScoreForNode.Clear();

            return route;
        }

        private void AddStep(DijkstraStep? previousStep, Node? activeNode, double cumulatedCost, int transportSequenceIndex, byte inboundMode, int inboundRouteType)
        {
            var exist = bestScoreForNode.ContainsKey(activeNode!.Idx);
            if (!exist || bestScoreForNode[activeNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = activeNode, CumulatedCost = cumulatedCost, TransportSequenceIndex = transportSequenceIndex, InboundTransportMode = inboundMode, InboundRouteType = inboundRouteType };
                dijkstraStepsQueue.Enqueue(step, cumulatedCost);

                if(!exist)
                {
                    bestScoreForNode.Add(activeNode.Idx, cumulatedCost);
                }
                else
                {
                    bestScoreForNode[activeNode.Idx] = cumulatedCost;
                }
            }
        }

        private void ReconstructRoute(DijkstraStep? currentStep, byte outboundTransportMode, int outboundRouteType)
        {
            if (currentStep != null)
            {
                ReconstructRoute(currentStep.PreviousStep,currentStep.InboundTransportMode,currentStep.InboundRouteType);
                route.Add(currentStep.ActiveNode!);
                if(currentStep.PreviousStep != null)
                {
                    if(currentStep.InboundTransportMode != outboundTransportMode && !transportModeTransitions.ContainsKey(currentStep.ActiveNode.Idx))
                    {
                        var transition = Tuple.Create<byte,int>(outboundTransportMode,outboundRouteType);
                        transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                    }
                }
                else
                {
                    var transition = Tuple.Create<byte,int>(outboundTransportMode,outboundRouteType);
                    transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                }
            }
        }
    }
}