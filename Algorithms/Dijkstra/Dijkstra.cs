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
                byte transportMode = transportModesSequence[0];
                Edge outboundEdge = originNode.GetFirstOutboundEdge(transportMode);
                if(outboundEdge != null)
                {
                    AddStep(null, originNode, 0, 0, transportMode, outboundEdge.TagIdRouteType);
                }
            }

            Node activeNode = new Node();

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                activeNode = currentStep!.ActiveNode;

                //debug:
                if(activeNode.Idx==1562551)
                {
                    Console.WriteLine("Probe 1562551 on Dijkstra.cs");
                }
                //

                int currentTransportIndex = currentStep.TransportSequenceIndex;
                byte currentTransportMask = transportModesSequence[currentTransportIndex];
                             
                if(activeNode == destinationNode)
                {
                    ReconstructRoute(currentStep,0);
                    routeCost = currentStep.CumulatedCost;
                    
                    break;
                }

                if(priority <= bestScoreForNode[activeNode!.Idx])
                {
                    foreach(var outwardEdge in activeNode.OutwardEdges)
                    {
                        var outboundModes = outwardEdge.TransportModes;

                        var currentMode = currentTransportMask;

                        //debug:
                        if(activeNode.Idx==1558840)
                        {
                            Console.WriteLine("Outward edge transport modes: {0}",TransportModes.MaskToString(outboundModes));
                        }
                        //
                        
                        if((outboundModes & currentMode) == currentMode)
                        {
                            var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, currentMode);
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex, currentMode, outwardEdge.TagIdRouteType);
                        }

                        // if((currentMode & TransportModes.PublicModes) != 0 && (outboundModes & TransportModes.DefaultMode) == TransportModes.DefaultMode)
                        // {
                        //     var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, TransportModes.DefaultMode);
                        //     AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex, TransportModes.DefaultMode, outwardEdge.TagIdRouteType);
                        // }

                        if(currentTransportIndex<sequenceLength-1)
                        {
                            byte nextTransportMode = transportModesSequence[currentTransportIndex+1];

                            if((outboundModes & nextTransportMode) == nextTransportMode)
                            {
                                var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, nextTransportMode);
                                AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex+1, nextTransportMode, outwardEdge.TagIdRouteType);
                                //AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex+1, transportMode, outwardEdge.TagIdRouteType);
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

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, int transportSequenceIndex, byte transportMode, int outboundRouteType)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, TransportSequenceIndex = transportSequenceIndex, TransportMode = transportMode, OutboundRouteType = outboundRouteType };
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

        private void ReconstructRoute(DijkstraStep? currentStep, int count)
        {
            if (currentStep != null)
            {
                if(currentStep.PreviousStep != null)
                {
                    if(currentStep.PreviousStep.TransportMode != currentStep.TransportMode && !transportModeTransitions.ContainsKey(currentStep.ActiveNode.Idx))
                    {
                        var transition = Tuple.Create<byte,int>(currentStep.TransportMode,currentStep.OutboundRouteType);
                        transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                        //debug:
                        Console.WriteLine("{0,3}   transition::: idx: {1,7} :: tm: {2,10} :: rt: {3,3} :: aitm: {4,50} :: aotm: {5,50}",
                                           count, currentStep.ActiveNode.Idx,
                                         TransportModes.SingleMaskToString(transition.Item1),
                                                                                          transition.Item2,
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableInboundTransportModes()),
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableOutboundTransportModes())
                                        );
                        //
                    }
                }
                else
                {
                    var transition = Tuple.Create<byte,int>(currentStep.TransportMode,currentStep.OutboundRouteType);
                    transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                    //debug:
                    Console.WriteLine("{0,3}   transition::: idx: {1,7} :: tm: {2,10} :: rt: {3,3} :: aitm: {4,50} :: aotm: {5,50}",
                                           count, currentStep.ActiveNode.Idx,
                                         TransportModes.SingleMaskToString(transition.Item1),
                                                                                          transition.Item2,
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableInboundTransportModes()),
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableOutboundTransportModes())
                                        );
                    //
                }
                ReconstructRoute(currentStep.PreviousStep, count+1);
                route.Add(currentStep.ActiveNode!);
                //debug:
                Console.WriteLine("{0,3}        route::: idx: {1,7} :: tm: {2,10} ::         :: aitm: {3,50} :: aotm: {4,50}",
                                   count,    currentStep.ActiveNode.Idx,
                            TransportModes.SingleMaskToString(currentStep.TransportMode),
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableInboundTransportModes()),
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableOutboundTransportModes())
                            );
                //
            }
        }
    }
}