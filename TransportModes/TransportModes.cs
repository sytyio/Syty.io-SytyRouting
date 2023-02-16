using NLog;
using SytyRouting.Model;

namespace SytyRouting
{
    public static class TransportModes
    {
        public const string NoTransportMode = "None";
        public const int MaxNumberOfTransportModes = sizeof(byte) * 8; // Number of bits to be used to identify the Transport Modes.
        public static byte DefaultMode;
        public static int NoRouteType = 0;

        public static int GtfsDefaultFoot = 13; // see on config file

        public static Dictionary<byte,byte> RoutingRules = new Dictionary<byte,byte>();
        public static byte None; // 'Empty' mask to handle errors and wrong results.
        public static byte PublicModes; // Mask of the public modes.
        public static Dictionary<int,double> RouteTypesToPenalties = new Dictionary<int,double>();
        public static Dictionary<byte,double> MasksToRoutingPenalties = new Dictionary<byte,double>();
        public static Dictionary<byte,double> MasksToSpeeds = new Dictionary<byte,double>();
        public static Dictionary<int,string> OSMTagIdToKeyValue = new Dictionary<int,string>();

        private static string[] Names = new string[1] {NoTransportMode};
        private static Dictionary<int,byte> Masks = new Dictionary<int,byte>();
        private static Dictionary<int,byte> RouteTypeToTransportModes = new Dictionary<int,byte>();
        
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static byte[] CreateSequence(string[] requestedModes)
        {
            byte[] requestedSequence = TransportModes.NamesToArray(requestedModes);
            
            return CreateSequence(requestedSequence);
        }

        public static byte[] CreateSequence(byte[] requestedSequence)
        {
            if(requestedSequence.Length==1 && requestedSequence[0]==DefaultMode)
            {
                return requestedSequence;
            }

            byte[] revisedSequence = ReviseSequence(requestedSequence);
            byte[] transportModeSequence = InsertDefaultMode(revisedSequence);

            return transportModeSequence;
        }

        private static byte[] ReviseSequence(byte[] requestedSequence)
        {
            byte[] cleanedSequence = RemoveConsecutiveDuplicates(
                                        RemoveDefaultMode(requestedSequence));

            List<byte> sequence = new List<byte>(0);
            if(cleanedSequence.Length>0)
            {
                sequence.Add(cleanedSequence[0]);
                int index=0;
                while(index<cleanedSequence.Length)
                {
                    byte currentMode=cleanedSequence[index++];
                    for(int i=index; i<cleanedSequence.Length; i++)
                    {
                        byte nextMode=cleanedSequence[index++];
                        if(RoutingRules.ContainsKey(currentMode))
                        {
                            byte alternativeModes=RoutingRules[currentMode];
                            if((nextMode & alternativeModes)==nextMode)
                            {
                                sequence.Add(nextMode);
                                index=i;
                                break;
                            }
                            else
                            {
                                logger.Debug("Invalid Transport Mode Sequence: {0} ---> {1}. Skipping {1}.\b",SingleMaskToString(currentMode),SingleMaskToString(nextMode));
                            }
                        }
                    }
                }
            }

            return RemoveConsecutiveDuplicates(sequence.ToArray()).ToArray();
        }

        private static byte[] RemoveDefaultMode(byte[] sequence)
        {
            List<byte> newSequence = new List<byte>(0);
            for(int i = 0; i < sequence.Length; i++)
            {
                if((sequence[i] & TransportModes.DefaultMode) != TransportModes.DefaultMode)
                {
                    newSequence.Add(sequence[i]);
                }
            }

            return newSequence.ToArray();
        }

        private static byte[] RemoveConsecutiveDuplicates(byte[] sequence)
        {
            List<byte> newSequence = new List<byte>(0);
            if(sequence.Length>0)
            {
                newSequence.Add(sequence[0]);
                for(int i = 1; i < sequence.Length; i++)
                {
                    if(sequence[i] != sequence[i-1])
                    {
                        newSequence.Add(sequence[i]);
                    }
                }
            }

            return newSequence.ToArray();
        }

        private static byte[] InsertDefaultMode(byte[] sequence)
        {
            byte[] newSequence = new byte[2*sequence.Length+1];

            if(sequence.Length>0)
            {
         
                int j=0;
                for(int i=0; i<sequence.Length; i++)
                {
                    newSequence[j]=DefaultMode;
                    newSequence[j+1]=sequence[i];
                    j+=2;
                }

                newSequence[newSequence.Length-1]=DefaultMode;
            }

            return newSequence;
        }

        public static byte ArrayToMask(byte[] transportModes)
        {
            byte result = 0;
            for(int i = 0; i < transportModes.Length; i++)
            {
                foreach(var transportModeMask in Masks)
                {
                    if(transportModeMask.Value != 0 && (transportModes[i] & transportModeMask.Value) == transportModeMask.Value)
                    {
                        result |= transportModeMask.Value;
                    }
                }
            }                
                
            return result;
        }

        public static string[] ArrayToNames(byte[] transportModes)
        {
            List<string> listResult = new List<string>(0);
            for(int i = 0; i < transportModes.Length; i++)
            {
                foreach(var transportModeMask in Masks)
                {
                    if(transportModeMask.Value != 0 && (transportModes[i] & transportModeMask.Value) == transportModeMask.Value)
                    {
                        listResult.Add(Names[transportModeMask.Key]);
                    }
                }
            }

            string[] result = listResult.ToArray();
            for(int i = 0; i < result.Length; i++)
            {
                if(result[i] == null)
                {
                    result[i] = TransportModes.NoTransportMode;
                }
            }
                
            return result;
        }

        public static int GtfsTypeToSpeed(int type){
            byte transportMode = RouteTypeToTransportModes[type];
            return (int)MasksToSpeeds[transportMode];
        }

        public static Dictionary<int,byte> CreateMasks(string[] transportModes)
        {
            SetNames(transportModes);

            // Create bitmasks for the Transport Modes based on the configuration data using a Dictionary.
            try
            {
                Masks.Add(0,0); // 0 -> No transport mode
                for(int n = 0; n < transportModes.Length-1; n++)
                {
                    var twoToTheNth = (byte)Math.Pow(2,n);
                    Masks.Add(n+1,twoToTheNth);
                }
            }
            catch (Exception e)
            {
                logger.Info("Transport Mode bitmask creation error: {0}", e.Message);
            }

            TransportModes.None = Masks[0];
            TransportModes.DefaultMode = Masks[1];
            logger.Info("Default Transport Mode (pedestrian): {0}. (First transport mode in the configuration file.)", SingleMaskToString(TransportModes.DefaultMode));

            return Masks;
        }

        private static Dictionary<byte,byte> CreateRoutingRules(TransportModeRoutingRule[] transportModeRoutingRules)
        {
            Dictionary<byte,byte> transportModeRoutingRoules = new Dictionary<byte,byte>();

            for(var i = 0; i < transportModeRoutingRules .Length; i++)
            {
                byte currentTransportModes = NamesToMask((transportModeRoutingRules[i].CurrentTransportModes));
                byte alternativeTransportModes = NamesToMask(transportModeRoutingRules[i].AlternativeTransportModes);
                if (!transportModeRoutingRoules.ContainsKey(currentTransportModes))
                {
                    transportModeRoutingRoules.Add(currentTransportModes, alternativeTransportModes);
                }
                else
                {
                    logger.Debug("Unable to add Transport Mode routing rule. Current Transport Modes: {0}", NamesToString(transportModeRoutingRules[i].CurrentTransportModes));
                }
            }
            
            return transportModeRoutingRoules;
        }
        
        private static Dictionary<int,byte> CreateMappingRouteTypeToTransportModes()
        {
            Dictionary<int,byte> routeTypeToTransportModes = new Dictionary<int,byte>();
            foreach(var routeType in Configuration.GtfsTypeToTransportModes)
            {
                if(!routeTypeToTransportModes.ContainsKey(routeType.RouteType))
                {
                    routeTypeToTransportModes.Add(routeType.RouteType,TransportModes.NamesToMask(routeType.AllowedTransportModes));
                }

            }

            return routeTypeToTransportModes;
        }

        public static async Task CreateMappingTagIdRouteTypeToTransportModes()
        {
            RouteTypeToTransportModes = await TransportModes.CreateMappingTagIdToTransportModes(Masks);
            var routeTypeToTransportModes = CreateMappingRouteTypeToTransportModes();
            foreach(var routeType in routeTypeToTransportModes)
            {
                if(!RouteTypeToTransportModes.ContainsKey(routeType.Key))
                    RouteTypeToTransportModes.Add(routeType.Key,routeType.Value);
                else
                    logger.Debug("Route Type or OSM TagId {0} already exists in the Route Type - Transport Modes dictionary", routeType.Key);
            }
        }

        private static async Task<Dictionary<int,byte>> CreateMappingTagIdToTransportModes(Dictionary<int,byte> transportModeMasks)
        {
            int[] configTagIds = await Configuration.ValidateOSMTags();

            Dictionary<int,byte> tagIdToTransportModes = new Dictionary<int,byte>();

            for(var i = 0; i < configTagIds.Length; i++)
            {
                byte mask = 0; // Default Transport Mode: 0

                var configAllowedTransportModes = Configuration.ValidateAllowedTransportModes(Configuration.OSMTags[i].AllowedTransportModes);
                foreach(var transportName in configAllowedTransportModes)
                {
                    int transportModeIndex = NameToIndex(transportName);
                    if(transportModeMasks.ContainsKey(transportModeIndex))
                    {
                        mask |= transportModeMasks[transportModeIndex];
                    }
                    else
                    {
                        logger.Info("Transport Mode '{0}' not found.",transportName);
                    }
                }
                if (!tagIdToTransportModes.ContainsKey(configTagIds[i]))
                {
                    tagIdToTransportModes.Add(configTagIds[i], mask);
                }
                else
                {
                    logger.Debug("Unable to add key to OSM-tag_id - to - Transport-Mode mapping. Tag id: {0}", configTagIds[i]);
                }
            }
            
            return tagIdToTransportModes;
        }

        public static void CreateMappingTagIdToKeyValue()
        {
            OSMTags[] tags = Configuration.OSMTags;

            Dictionary<int,string> tagIdToKeyValue = new Dictionary<int,string>();
            
            foreach(var tag in tags)
            {
                if (!tagIdToKeyValue.ContainsKey(tag.Id))
                {
                    tagIdToKeyValue.Add(tag.Id, tag.Key + " : " + tag.Value);
                }
                else
                {
                    logger.Debug("Unable to add key to OSM TagId - to - OSM TagKeyValue. Tag id: {0}", tag.Id);
                }
            }
            
            OSMTagIdToKeyValue = tagIdToKeyValue;
        }

        public static void CreateMappingTagIdRouteTypeToRoutingPenalty()
        {
            Dictionary<int,double> tagIdRouteTypeToRoutingPenalities = new Dictionary<int,double>();
            
            for(var i = 0; i < Configuration.GtfsTypeToTransportModes.Length; i++)
            {
                var routeType =  Configuration.GtfsTypeToTransportModes[i].RouteType;
                var penalty = Configuration.GtfsTypeToTransportModes[i].RoutingPenalty;
                if(!tagIdRouteTypeToRoutingPenalities.ContainsKey(routeType))
                {
                    tagIdRouteTypeToRoutingPenalities.Add(routeType,penalty);
                }
                else
                {
                    logger.Debug("Routing penalty for the GTFS route {0} has already bee set to {1}.", routeType, tagIdRouteTypeToRoutingPenalities[routeType]);
                }
            }

            for(var i = 0; i < Configuration.OSMTags.Length; i++)
            {
                var tagId =  Configuration.OSMTags[i].Id;
                var penalty = Configuration.OSMTags[i].RoutingPenalty;
                if(!tagIdRouteTypeToRoutingPenalities.ContainsKey(tagId))
                {
                    tagIdRouteTypeToRoutingPenalities.Add(tagId,penalty);
                }
                else
                {
                    logger.Debug("Routing penalty for the OSM TagId {0} has already bee set to {1}.", tagId, tagIdRouteTypeToRoutingPenalities[tagId]);
                }
            }

            RouteTypesToPenalties = tagIdRouteTypeToRoutingPenalities;
        }

        public static void CreateMappingMaskToRoutingPenalty()
        {
            Dictionary<byte,double> transportModeMaskToRoutingPenalties = new Dictionary<byte,double>();
            
            foreach(var mask in Masks)
            {
                if(mask.Key!=0)
                {
                    var penalty = Configuration.TransportModeRoutingPenalties[mask.Key];
                    if(!transportModeMaskToRoutingPenalties.ContainsKey(mask.Value))
                    {
                        transportModeMaskToRoutingPenalties.Add(mask.Value,penalty);
                    }
                    else
                    {
                        logger.Debug("Routing penalty for the transport mode {0} has already bee set to {1}.", MaskToString(mask.Value), transportModeMaskToRoutingPenalties[mask.Value]);
                    }
                }
            }

            TransportModes.MasksToRoutingPenalties = transportModeMaskToRoutingPenalties;
        }

        public static void CreateMappingMasksToMaxSpeeds()
        {
            Dictionary<byte,double> transportModeMasksToSpeeds = new Dictionary<byte,double>();
            
            foreach(var mask in Masks)
            {
                if(mask.Key!=0)
                {
                    var speed = Configuration.TransportModeSpeeds[mask.Key];
                    if(!transportModeMasksToSpeeds.ContainsKey(mask.Value))
                    {
                        transportModeMasksToSpeeds.Add(mask.Value,speed);
                    }
                    else
                    {
                        logger.Debug("Maximum speed for the transport mode {0} has already bee set to {1} [km/h] .", MaskToString(mask.Value), Helper.MPerSToKMPerHour(transportModeMasksToSpeeds[mask.Value]));
                    }
                }
            }

            TransportModes.MasksToSpeeds = transportModeMasksToSpeeds;
        }

        public static void DisplayMaskMap()
        {
            foreach(var mask in Masks)
            {
                logger.Debug("{0,1} :: {1,3} :: {2,-20}", mask.Key, mask.Value, SingleMaskToString(mask.Value));
            }
        }

        public static byte[] GetTransportModes()
        {
            var transportMasks = Masks.Values.ToArray().Where(m=>m!=0).ToArray();
            
            return transportMasks;
        }

        public static int[] GetRouteTypes()
        {
            var routeTypes = RouteTypeToTransportModes.Keys.ToArray().Where(k=>k!=0).ToArray();
            
            return routeTypes;
        }

        public static void LoadRoutingRules(TransportModeRoutingRule[] transportModeRoutingRules)
        {
            RoutingRules = TransportModes.CreateRoutingRules(transportModeRoutingRules);
        }
        
        public static byte[] MaskToArray(byte transportModes)
        {   
            return MaskToList(transportModes).ToArray();
        }

        public static List<byte> MaskToList(byte transportModes)
        {
            List<byte> result = new List<byte>(0);
            foreach(var transportModeMask in Masks)
            {
                if(transportModeMask.Value != 0 && (transportModes & transportModeMask.Value) == transportModeMask.Value)
                {
                    result.Add(transportModeMask.Value);
                }
            }
                
            return result;
        }

        public static string MaskToString(byte transportModes)
        {
            string result = "";
            foreach(var transportModeMask in Masks)
            {
                if(transportModeMask.Value != 0 && (transportModes & transportModeMask.Value) == transportModeMask.Value)
                {
                    result += Names[transportModeMask.Key] + " ";
                }
            }
                
            return (result == "")? TransportModes.NoTransportMode : result;
        }

        public static string SingleMaskToString(byte transportMode)
        {
            string result = "";
            foreach(var transportModeMask in Masks)
            {
                if(transportModeMask.Value != 0 && (transportMode & transportModeMask.Value) == transportModeMask.Value)
                {
                    result = Names[transportModeMask.Key];
                    break;
                }
            }
                
            return (result == "")? TransportModes.NoTransportMode : result;
        }

        public static byte[] NamesToArray(string[] transportModesSequence)
        {
            List<byte> masksList = new List<byte>(0);

            for(int i = 0; i < transportModesSequence.Length; i++)
            {
                bool nameFound=false;
                for(int j = 0; j < Names.Length; j++)
                {
                    if(transportModesSequence[i].Equals(Names[j]))
                    {
                        masksList.Add(Masks[j]);
                        nameFound=true;
                        break;
                    }
                }
                if(!nameFound)
                    logger.Info("Transport Mode Sequence: Transport mode '{0}' not found. Ignoring transport mode.", transportModesSequence[i]);
            }
                
            return masksList.ToArray();
        }

        public static int NameToIndex(string transportModeName)
        {
            for(int i = 1; i < Names.Length; i++)
            {
                if(Names[i].Equals(transportModeName))
                {
                    return i;
                }
            }

            return 0;
        }

        public static byte NameToMask(string transportModeName)
        {
            var key = NameToIndex(transportModeName);
            if(Masks.ContainsKey(key))
            {
                return Masks[key];
            }
            else
            {
                logger.Info("Transport mode name {0} not found in the validated list of transport modes. (Transport configuration file.)", transportModeName);
                return 0;
            }
        }
        
        public static byte NamesToMask(string[] transportModeNames)
        {
            byte transportModesMask = 0;
            for(int i = 0; i < transportModeNames.Length; i++)
            {
                int transportModeIndex = NameToIndex(transportModeNames[i]);
                if(transportModeIndex != 0)
                {
                    transportModesMask |= Masks[transportModeIndex];
                }   
                else
                {
                    logger.Info("Transport mode name '{0}' not found in the validated list of transport modes.", transportModeNames[i]);
                }
            }
            return transportModesMask;
        }

        public static string NamesToString(string[] transportModeNames)
        {
            string transportModeNamesString = "";
            if(transportModeNames.Length>=1)
            {
                for(int i = 0; i < transportModeNames.Length-1; i++)
                {
                    transportModeNamesString += transportModeNames[i] + ", ";
                }
                transportModeNamesString += transportModeNames[transportModeNames.Length-1];
            }
            return transportModeNamesString;
        }

        public static byte RoutingRulesContainKey(byte transportMask)
        {
            var rulesKeys = RoutingRules.Keys;
            foreach(var mask in rulesKeys)
            {
                var maskArray = MaskToArray(mask);
                for(var i = 0; i < maskArray.Length; i++)
                {
                    if((maskArray[i] & transportMask) == transportMask)
                    {
                        return mask;
                    }
                }
            }
            return 0;
        }

        private static void SetNames(string[] transportModes)
        {
            try
            {
                Array.Resize(ref Names, transportModes.Length);
                for(int i = 0; i < transportModes.Length; i++)
                {
                    Names[i] = transportModes[i];
                }
            }
            catch (Exception e)
            {
                logger.Info("Initialization error in the creation of the Transport Mode List: {0}", e.Message);
            }
        }

        public static void SetPublicModes(string[] publicTransportModes)
        {
            PublicModes = TransportModes.ArrayToMask(TransportModes.NamesToArray(publicTransportModes));
        }

        public static void SetPublicModes(byte publicTransportModes)
        {
            PublicModes = publicTransportModes;
        }

        public static byte TagIdToTransportModes(int tagId)
        {
            if (RouteTypeToTransportModes.ContainsKey(tagId))
            {
                return RouteTypeToTransportModes[tagId];
            }
            else
            {
                logger.Info("Unable to find OSM tag_id {0} in the tag_id-to-Transport Mode mapping. Transport Mode set to 'None'", tagId);
                return (byte)0; // Transport Mode: 0 ("None");
            }
        }
    }
}