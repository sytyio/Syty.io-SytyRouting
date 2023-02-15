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
                //debug: byte transportMode = transportModesSequence[0];
                byte outboundMode  = transportModesSequence.First(); // requested mode
                Edge outboundEdge = originNode.GetFirstOutboundEdge(outboundMode);
                if(outboundEdge != null)
                {
                    byte inboundMode = TransportModes.None; // inbound transport mode (wrt the step active node)
                    var inboundRouteType = TransportModes.NoRouteType;
                    //debug: AddStep(null, originNode, 0, 0, transportMode, outboundEdge.TagIdRouteType);
                    AddStep(null, originNode, 0, 0, inboundMode, inboundRouteType);
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

                int transportIndex = currentStep.TransportSequenceIndex;
                byte inboundMode = currentStep.InboundTransportMode;
                byte outboundMode = transportModesSequence[transportIndex];
                             
                if(activeNode == destinationNode)
                {
                    //debug:
                    int initialCount=0;
                    TestBench.DisplayRouteReconstruction(currentStep);
                    //
                    byte outboundTransportMode=TransportModes.None;
                    int outboundRouteType=TransportModes.NoRouteType;
                    ReconstructRoute(currentStep,outboundTransportMode,outboundRouteType,initialCount);
                    routeCost = currentStep.CumulatedCost;
                    
                    break;
                }

                if(priority <= bestScoreForNode[activeNode!.Idx])
                {
                    foreach(var outwardEdge in activeNode.OutwardEdges)
                    {
                        var availableOutboundModes = outwardEdge.TransportModes;

                        var currentMode = inboundMode;

                        //debug:
                        // var probe=1558840;
                        var probe1=1562550;
                        if(activeNode.Idx==probe1)
                        {
                            Console.WriteLine("Probe {0}: Outward edge {1}: Source: {2} -> transport modes: {3} -> Target: {4}",probe1,outwardEdge.OsmID,outwardEdge.SourceNode.Idx,TransportModes.MaskToString(availableOutboundModes),outwardEdge.TargetNode.Idx);
                        }
                        var probe2=1562550;
                        if(outwardEdge.TargetNode.Idx==probe2)
                        {
                            Console.WriteLine("Probe {0}: Outward edge {1}: Source: {2} -> transport modes: {3} -> Target: {4}",probe1,outwardEdge.OsmID,outwardEdge.SourceNode.Idx,TransportModes.MaskToString(availableOutboundModes),outwardEdge.TargetNode.Idx);
                        }
                        //
                        
                        if((availableOutboundModes & outboundMode) == outboundMode)
                        {
                            var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, outboundMode);
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, transportIndex, outboundMode, outwardEdge.TagIdRouteType);
                        }

                        // if((currentMode & TransportModes.PublicModes) != 0 && (outboundModes & TransportModes.DefaultMode) == TransportModes.DefaultMode)
                        // {
                        //     var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, TransportModes.DefaultMode);
                        //     AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + cost, currentTransportIndex, TransportModes.DefaultMode, outwardEdge.TagIdRouteType);
                        // }

                        if(transportIndex<sequenceLength-1)
                        {
                            byte nextTransportMode = transportModesSequence[transportIndex+1];

                            if((availableOutboundModes & nextTransportMode) == nextTransportMode)
                            {
                                var cost = Helper.ComputeEdgeCost(CostCriteria.MinimalTravelTime, outwardEdge, nextTransportMode);
                                //AddStep(currentStep.PreviousStep, outwardEdge.SourceNode, currentStep.PreviousStep.CumulatedCost + cost, transportIndex+1, nextTransportMode, outwardEdge.TagIdRouteType);
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

        //debug: private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, int transportSequenceIndex, byte transportMode, int outboundRouteType)
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

        // private void ReconstructRoute(DijkstraStep? currentStep, int count)
        // {
        //     if (currentStep != null)
        //     {
        //         if(currentStep.PreviousStep != null)
        //         {
        //             if(currentStep.PreviousStep.TransportMode != currentStep.TransportMode && !transportModeTransitions.ContainsKey(currentStep.ActiveNode.Idx))
        //             {
        //                 var transition = Tuple.Create<byte,int>(currentStep.TransportMode,currentStep.InboundRouteType);
        //                 transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
        //                 //debug:
        //                 Console.WriteLine("{0,3}   transition::: idx: {1,7} :: tm: {2,10} :: rt: {3,3} :: aitm: {4,50} :: aotm: {5,50}",
        //                                    count, currentStep.ActiveNode.Idx,
        //                                  TransportModes.SingleMaskToString(transition.Item1),
        //                                                                                   transition.Item2,
        //                                                                TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableInboundTransportModes()),
        //                                                                TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableOutboundTransportModes())
        //                                 );
        //                 //
        //             }
        //         }
        //         else
        //         {
        //             var transition = Tuple.Create<byte,int>(currentStep.TransportMode,currentStep.InboundRouteType);
        //             transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
        //             //debug:
        //             Console.WriteLine("{0,3}   transition::: idx: {1,7} :: tm: {2,10} :: rt: {3,3} :: aitm: {4,50} :: aotm: {5,50}",
        //                                    count, currentStep.ActiveNode.Idx,
        //                                  TransportModes.SingleMaskToString(transition.Item1),
        //                                                                                   transition.Item2,
        //                                                                TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableInboundTransportModes()),
        //                                                                TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableOutboundTransportModes())
        //                                 );
        //             //
        //         }
        //         ReconstructRoute(currentStep.PreviousStep, count+1);
        //         route.Add(currentStep.ActiveNode!);
        //         //debug:
        //         Console.WriteLine("{0,3}        route::: idx: {1,7} :: tm: {2,10} ::         :: aitm: {3,50} :: aotm: {4,50}",
        //                            count,    currentStep.ActiveNode.Idx,
        //                     TransportModes.SingleMaskToString(currentStep.TransportMode),
        //                                                                TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableInboundTransportModes()),
        //                                                                TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableOutboundTransportModes())
        //                     );
        //         //
        //     }
        // }

        private void ReconstructRoute(DijkstraStep? currentStep, byte outboundTransportMode, int outboundRouteType, int count)
        {
            if (currentStep != null)
            {
                ReconstructRoute(currentStep.PreviousStep,currentStep.InboundTransportMode,currentStep.InboundRouteType,count+1);
                route.Add(currentStep.ActiveNode!);
                if(currentStep.PreviousStep != null)
                {
                    if(currentStep.InboundTransportMode != outboundTransportMode && !transportModeTransitions.ContainsKey(currentStep.ActiveNode.Idx))
                    {
                        var transition = Tuple.Create<byte,int>(outboundTransportMode,outboundRouteType);
                        transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                        //debug:
                        Console.WriteLine("count: {0,3}   transition::: node idx: {1,7} :: outbound transport mode(s): {2,10} :: outbound route type: {3,3} :: available inbound modes at node: {4,50} :: available outbound modes at node: {5,50}",
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
                    var transition = Tuple.Create<byte,int>(outboundTransportMode,outboundRouteType);
                    transportModeTransitions.Add(currentStep.ActiveNode.Idx, transition);
                    //debug:
                    Console.WriteLine("count: {0,3}   transition::: node idx: {1,7} :: outbound transport mode(s): {2,10} :: outbound route type: {3,3} :: available inbound modes at node: {4,50} :: available outbound modes at node: {5,50}",
                                           count, currentStep.ActiveNode.Idx,
                                         TransportModes.SingleMaskToString(transition.Item1),
                                                                                          transition.Item2,
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableInboundTransportModes()),
                                                                       TransportModes.MaskToString(currentStep.ActiveNode.GetAvailableOutboundTransportModes())
                                        );
                    //
                }
            }
        }
    }
}