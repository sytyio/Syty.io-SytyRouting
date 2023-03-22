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

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode, byte[] requestedModes)
        {
            route.Clear();
            transportModeTransitions.Clear();
            routeCost = 0;
            TrasnportModeSequence = requestedModes;

            var sequenceLength = requestedModes.Length;
           
            if(sequenceLength>0)
            {
                byte outboundMode  = requestedModes.First(); // first requested mode
                Edge outboundEdge = originNode.GetFirstOutboundEdge(outboundMode);
                if(outboundEdge != null)
                {
                    //byte inboundMode = TransportModes.None; // inbound transport mode (wrt the step active node)
                    //var inboundRouteType = TransportModes.NoRouteType;
                    var outboundRouteType = outboundEdge.TagIdRouteType;
                    //AddStep(null, originNode, 0, 0, inboundMode, inboundRouteType);
                    AddStep(null, originNode, 0, 0, outboundMode, outboundRouteType);
                }
            }

            Node activeNode = new Node();

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                activeNode = currentStep!.ActiveNode;

                int transportIndex = currentStep.TransportSequenceIndex;
                //byte inboundMode = currentStep.PreviousStep.OutboundTransportMode;
                //byte outboundMode = transportModesSequence[transportIndex];
                byte outboundMode = currentStep.OutboundTransportMode;
                             
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
                        //var currentMode = inboundMode;
                        
                        if((availableOutboundModes & outboundMode) == outboundMode)
                        {
                            var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, outboundMode);
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, transportIndex, outboundMode, outwardEdge.TagIdRouteType);
                        }

                        if(transportIndex<sequenceLength-1)
                        {
                            byte nextTransportMode = requestedModes[transportIndex+1];

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

        private void AddStep(DijkstraStep? previousStep, Node? activeNode, double cumulatedCost, int transportSequenceIndex, byte outboundMode, int outboundRouteType)
        {
            var exist = bestScoreForNode.ContainsKey(activeNode!.Idx);
            if (!exist || bestScoreForNode[activeNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = activeNode, CumulatedCost = cumulatedCost, TransportSequenceIndex = transportSequenceIndex, OutboundTransportMode = outboundMode, OutboundRouteType = outboundRouteType };
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

        private void ReconstructRoute(DijkstraStep? currentStep, byte outboundMode, int outboundRouteType)
        {
            if (currentStep != null)
            {
                ReconstructRoute(currentStep.PreviousStep,currentStep.OutboundTransportMode,currentStep.OutboundRouteType);
                route.Add(currentStep.ActiveNode!);
                if(currentStep.PreviousStep != null)
                {
                    if(currentStep.OutboundTransportMode != outboundMode && !transportModeTransitions.ContainsKey(currentStep.ActiveNode.Idx))
                    {
                        var transition = Tuple.Create<byte,int>(outboundMode,outboundRouteType);
                        transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                    }
                }
                else
                {
                    var transition = Tuple.Create<byte,int>(outboundMode,outboundRouteType);
                    transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                }
            }
        }
    }
}