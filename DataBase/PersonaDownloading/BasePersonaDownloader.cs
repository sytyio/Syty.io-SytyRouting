using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.Geometries;
using NLog;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public abstract class BasePersonaDownloader : IPersonaDownloader
    {
        [NotNull]
        protected Graph? _graph = null!;
        protected string _connectionString = null!;
        protected string _personaTable = null!;
        protected int sequenceValidationErrors = 0;

        private static Logger logger = LogManager.GetCurrentClassLogger();


        public void Initialize(Graph graph, string connectionString, string personaTable)
        {
            _graph = graph;
            _connectionString = connectionString;
            _personaTable = personaTable;
        }
        
        protected byte[] ValidateTransportSequence(int id, Point homeLocation, Point workLocation, string[] transportSequence)
        {
            byte[] formattedSequence = TransportModes.CreateSequence(transportSequence);

            byte initialTransportMode = formattedSequence.First();
            byte finalTransportMode = formattedSequence.Last();

            var originNode = _graph.GetNodeByLongitudeLatitude(homeLocation.X, homeLocation.Y, isSource: true);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(workLocation.X, workLocation.Y, isTarget: true);

            Edge outboundEdge = originNode.GetFirstOutboundEdge(initialTransportMode);
            Edge inboundEdge = destinationNode.GetFirstInboundEdge(finalTransportMode);

            if(outboundEdge != null && inboundEdge != null)
            {
                return formattedSequence;
            }
            else
            {
                sequenceValidationErrors++;

                logger.Debug("!===================================!");
                logger.Debug(" Transport sequence validation error:");
                logger.Debug("!===================================!");
                logger.Debug(" Persona Id: {0}", id);
                if(outboundEdge == null)
                {
                    logger.Debug(" ORIGIN Node Idx {0} does not contain the requested transport mode '{1}'.",originNode.Idx,TransportModes.SingleMaskToString(initialTransportMode)); // Use MaskToString() if the first byte in the sequence represents more than one Transport Mode

                    var outboundEdgeTypes = originNode.GetOutboundEdgeTypes();
                    string outboundEdgeTypesS = "";
                    foreach(var edgeType in outboundEdgeTypes)
                    {
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(edgeType))
                            outboundEdgeTypesS += edgeType.ToString() +  " " + TransportModes.OSMTagIdToKeyValue[edgeType] + " ";
                    }
                    logger.Debug(" Outbound Edge type(s): {0}",outboundEdgeTypesS);
                    
                    var outboundTransportModes = originNode.GetAvailableOutboundTransportModes();
                    logger.Debug(" Available outbound transport modes at origin: {0}",TransportModes.MaskToString(outboundTransportModes));
                    initialTransportMode = TransportModes.MaskToArray(outboundTransportModes).First();
                }

                if(inboundEdge == null)
                {
                    logger.Debug(" DESTINATION Node Idx {0} does not contain the requested transport mode '{1}'.",destinationNode.Idx,TransportModes.SingleMaskToString(finalTransportMode));

                    var inboundEdgeTypes = destinationNode.GetInboundEdgeTypes();
                    string inboundEdgeTypesS = "";
                    foreach(var edgeType in inboundEdgeTypes)
                    {
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(edgeType))
                            inboundEdgeTypesS += edgeType.ToString() +  " " + TransportModes.OSMTagIdToKeyValue[edgeType] + " ";
                    }
                    logger.Debug(" Inbound Edge type(s): {0}",inboundEdgeTypesS);
                    
                    var inboundTransportModes = destinationNode.GetAvailableInboundTransportModes();
                    logger.Debug(" Available inbound transport modes at destination: {0}",TransportModes.MaskToString(inboundTransportModes));
                    finalTransportMode = TransportModes.MaskToArray(inboundTransportModes).First();
                }
            
                var newSequence = new byte[2];
                newSequence[0] = initialTransportMode;
                newSequence[1] = finalTransportMode;
                logger.Debug(" Requested transport sequence overridden to: {0}", TransportModes.ArrayToNames(newSequence));
                
                return newSequence;
            }
        }

        public int GetValidationErrors()
        {
            return sequenceValidationErrors;
        }
        
        public int[] GetBatchSizes(int regularBatchSize, int elementsToProcess)
        {
            var batchSize = (regularBatchSize > elementsToProcess) ? elementsToProcess : regularBatchSize;
            var numberOfBatches = (elementsToProcess / batchSize > 0) ? elementsToProcess / batchSize : 1;

            return GetBatchPartition(batchSize, elementsToProcess, numberOfBatches);
        }

        public int[] GetBatchPartition(int regularSlice, int whole, int numberOfSlices)
        {
            int lastSlice = whole - regularSlice * (numberOfSlices - 1);
            int[] batchPartition = new int[numberOfSlices];
            for (var i = 0; i < batchPartition.Length-1; i++)
            {
                batchPartition[i] = regularSlice;
            }
            batchPartition[batchPartition.Length-1] = lastSlice;

            return batchPartition;
        }

        public virtual Task<Persona[]> DownloadPersonasAsync(string connectionString, string personaTable, int batchSize, int offset)
        {
           throw new NotImplementedException();
        }
    }
}