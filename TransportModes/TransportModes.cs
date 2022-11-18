using NLog;
using SytyRouting.Model;

namespace SytyRouting
{
    public static class TransportModes
    {
        public const string NoTransportMode = "None";
        public const int MaxNumberOfTransportModes = sizeof(byte) * 8; // Number of bits to be used to identify the Transport Modes.

        public static byte DefaultMode;

        public static Dictionary<byte,byte> RoutingRules = new Dictionary<byte,byte>();        
        public static Dictionary<int,byte> TransportModeMasks = new Dictionary<int,byte>();
        public static byte PublicTransportModes; // mask of the public modes.
        public static byte PublicTransportGroup; // Independent of the above mask. Only for reference in transport mode sequences and transport modes routing rules.
        private static Dictionary<int,byte> OSMTagIdToTransportModes = new Dictionary<int,byte>();
        private static string[] TransportModeNames = new string[1] {NoTransportMode};

        
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static byte[] CreateTransportModeSequence(string[] requestedTransportModes)
        {
            byte[] requestedSequence = TransportModes.NameSequenceToMasksArray(requestedTransportModes);
            
            return CreateTransportModeSequence(requestedSequence);
        }

        public static byte[] CreateTransportModeSequence(byte[] requestedTransportModes)
        {
            byte[] revisedTransportModeSequence = ReviseTransportModeSequence(requestedTransportModes);
            byte[] transportModeSequence = AddDefaultTransportModeToSequenceEnds(revisedTransportModeSequence);

            return transportModeSequence;
        }

        public static bool ValidateTransportModeSequence(Node origin, Node destination, byte[] transportModeSequence)
        {
            bool theRouteEndsAreValid = RouteEndsAreValid(origin, destination, transportModeSequence);

            if(theRouteEndsAreValid)
            {
                return true;
            }

            return false;
        }

        private static bool RouteEndsAreValid(Node origin, Node destination, byte[] transportModesSequence)
        {
            bool theOriginIsValid = origin.IsAValidRouteStart(transportModesSequence);
            bool theDestinationIsValid = destination.IsAValidRouteEnd(transportModesSequence);

            if(theOriginIsValid && theDestinationIsValid)
            {
                return true;
            }

            return false;
        }

        private static byte[] ReviseTransportModeSequence(byte[] requestedTransportModes)
        {
            byte[] cleanedSequence = RemoveConsecutiveDuplicatesFromTransportModeSequence(
                                        RemoveDefaultTransportModeFromSequence(requestedTransportModes));


            List<byte> transportModeSequence = new List<byte>(0);
            if(cleanedSequence.Length>0)
            {
                transportModeSequence.Add(cleanedSequence[0]);
                int index = 0;
                while(index < cleanedSequence.Length)
                {
                    byte currentTransportMode = cleanedSequence[index++];
                    for(int i = index; i < cleanedSequence.Length; i++)
                    {
                        byte nextTransportMode = cleanedSequence[index++];
                        if(RoutingRules.ContainsKey(currentTransportMode))
                        {
                            byte alternativeTransportModes = RoutingRules[currentTransportMode];
                            if((nextTransportMode & alternativeTransportModes) == nextTransportMode)
                            {
                                transportModeSequence.Add(nextTransportMode);
                                index = i;
                                break;
                            }
                            else
                            {
                                logger.Debug("Invalid Transport Mode Sequence: {0}---> {1}:: Skipping {1}\b", MaskToString(currentTransportMode),MaskToString(nextTransportMode));
                            }
                        }
                    }
                }
            }

            return transportModeSequence.ToArray();
        }

        private static byte[] RemoveDefaultTransportModeFromSequence(byte[] transportModeSequence)
        {
            List<byte> newSequence = new List<byte>(0);
            for(int i = 0; i < transportModeSequence.Length; i++)
            {
                if((transportModeSequence[i] & TransportModes.DefaultMode) != TransportModes.DefaultMode)
                {
                    newSequence.Add(transportModeSequence[i]);
                }
            }

            return newSequence.ToArray();
        }

        private static byte[] RemoveConsecutiveDuplicatesFromTransportModeSequence(byte[] transportModeSequence)
        {
            List<byte> newSequence = new List<byte>(0);
            if(transportModeSequence.Length>0)
            {
                newSequence.Add(transportModeSequence[0]);
                for(int i = 1; i < transportModeSequence.Length; i++)
                {
                    if(transportModeSequence[i] != transportModeSequence[i-1])
                    {
                        newSequence.Add(transportModeSequence[i]);
                    }
                }
            }

            return newSequence.ToArray();
        }

        private static byte[] AddDefaultTransportModeToSequenceEnds(byte[] transportModeSequence)
        {
            byte[] newSequence = new byte[transportModeSequence.Length];
            if(transportModeSequence.Length>0)
            {
                if(transportModeSequence[0]!=TransportModes.DefaultMode)
                {
                    Array.Resize(ref newSequence,newSequence.Length+1);
                    newSequence[0]=TransportModes.DefaultMode;
                    for(int i=0; i<transportModeSequence.Length; i++)
                    {
                        newSequence[i+1]=transportModeSequence[i];
                    }
                }
                if(transportModeSequence.Last()!=TransportModes.DefaultMode)
                {
                    Array.Resize(ref newSequence,newSequence.Length+1);
                    newSequence[newSequence.Length-1]=TransportModes.DefaultMode;
                }
            }

            return newSequence;
        }

        public static byte GetMaskFromNames(string[] transportModeNames)
        {
            byte transportModesMask = 0;
            for(int i = 0; i < transportModeNames.Length; i++)
            {
                int transportModeIndex = TransportModes.GetTransportModeNameIndex(transportModeNames[i]);
                if(transportModeIndex != 0)
                {
                    transportModesMask |= TransportModeMasks[transportModeIndex];
                }   
                else
                {
                    logger.Info("Transport mode name '{0}' not found in the validated list of transport modes.", transportModeNames[i]);
                }
            }
            return transportModesMask;
        }

        public static byte GetTransportModeMask(string transportModeName)
        {
            var key = GetTransportModeNameIndex(transportModeName);
            if(TransportModeMasks.ContainsKey(key))
            {
                return TransportModeMasks[key];
            }
            else
            {
                logger.Info("Transport mode name {0} not found in the validated list of transport modes. (Transport configuration file.)", transportModeName);
                return 0;
            }
        }
        
        public static byte GetTransportModes(int tagId,Dictionary<int,byte> tagIdToTransportMode)
        {
            if (tagIdToTransportMode.ContainsKey(tagId))
            {
                return tagIdToTransportMode[tagId];
            }
            else
            {
                logger.Info("Unable to find OSM tag_id {0} in the tag_id-to-Transport Mode mapping. Transport Mode set to 'None'", tagId);
                return (byte)0; // Default Ttransport Mode: 0 ("None");
            }
        }

        public static string NamesToString(Dictionary<int,string> transportModeNames)
        {
            var transportModeNamesArray = transportModeNames.Values.ToArray();
            return NamesToString(transportModeNamesArray);
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

        public static string MaskToString(byte transportModes)
        {
            string result = "";
            foreach(var transportModeMask in TransportModeMasks)
            {
                if(transportModeMask.Value != 0 && (transportModes & transportModeMask.Value) == transportModeMask.Value)
                {
                    result += TransportModeNames[transportModeMask.Key] + " ";
                }
            }
                
            return (result == "")? TransportModes.NoTransportMode : result;
        }

        public static byte[] MaskToArray(byte transportModes)
        {
            List<byte> result = new List<byte>(0);
            foreach(var transportModeMask in TransportModeMasks)
            {
                if(transportModeMask.Value != 0 && (transportModes & transportModeMask.Value) == transportModeMask.Value)
                {
                    result.Add(transportModeMask.Value);
                }
            }
                
            return result.ToArray();
        }

        public static byte ArrayToMask(byte[] transportModes)
        {
            byte result = 0;
            for(int i = 0; i < transportModes.Length; i++)
            {
                foreach(var transportModeMask in TransportModeMasks)
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
            string[] result = new string[transportModes.Length];
            for(int i = 0; i < transportModes.Length; i++)
            {
                foreach(var transportModeMask in TransportModeMasks)
                {
                    if(transportModeMask.Value != 0 && (transportModes[i] & transportModeMask.Value) == transportModeMask.Value)
                    {
                        result[i] = TransportModeNames[transportModeMask.Key];
                    }
                }
            }
            for(int i = 0; i < result.Length; i++)
            {
                if(result[i] == null)
                {
                    result[i] = TransportModes.NoTransportMode;
                }
            }
                
            return result;
        }

        public static byte[] NameSequenceToMasksArray(string[] transportModesSequence)
        {
            List<byte> masksList = new List<byte>(0);

            for(int i = 0; i < transportModesSequence.Length; i++)
            {
                bool nameFound=false;
                for(int j = 0; j < TransportModeNames.Length; j++)
                {
                    if(transportModesSequence[i].Equals(TransportModeNames[j]))
                    {
                        masksList.Add(TransportModeMasks[j]);
                        nameFound=true;
                        break;
                    }
                }
                if(!nameFound)
                    logger.Info("Transport Mode Sequence: Transport mode '{0}' not found. Ignoring transport mode.", transportModesSequence[i]);
            }
                
            return masksList.ToArray();
        }

        public static byte FirstTransportModeFromMask(byte transportModes)
        {
            byte result = 0;
            foreach(var transportModeMask in TransportModeMasks)
            {
                if(transportModeMask.Value != 0 && (transportModes & transportModeMask.Value) == transportModeMask.Value)
                {
                    result = transportModeMask.Value;
                    break;
                }
            }
                
            return result;
        }

        public static byte GetTransportModesForTagId(int tagId)
        {
            if (OSMTagIdToTransportModes.ContainsKey(tagId))
            {
                return OSMTagIdToTransportModes[tagId];
            }
            else
            {
                logger.Info("Unable to find OSM tag_id {0} in the tag_id-to-Transport Mode mapping. Transport Mode set to 'None'", tagId);
                return (byte)0; // Transport Mode: 0 ("None");
            }
        }

        public static string TransportModesToString(byte transportModes)
        {
            string result = "";
            foreach(var tmm in TransportModeMasks)
            {
                if(tmm.Value != 0 && (transportModes & tmm.Value) == tmm.Value)
                {
                    result += tmm.Key + " ";
                }
            }
                
            return (result == "")? TransportModes.NoTransportMode : result;
        }

        public static Dictionary<int,byte> CreateTransportModeMasks(string[] transportModes)
        {
            SetTransportModeNames(transportModes);

            // Create bitmasks for the Transport Modes based on the configuration data using a Dictionary.
            try
            {
                TransportModeMasks.Add(0,0);
                for(int n = 0; n < transportModes.Length-1; n++)
                {
                    var twoToTheNth = (byte)Math.Pow(2,n);
                    TransportModeMasks.Add(n+1,twoToTheNth);
                }
            }
            catch (Exception e)
            {
                logger.Info("Transport Mode bitmask creation error: {0}", e.Message);
            }

            TransportModes.DefaultMode = TransportModeMasks[1];
            logger.Info("Default Transport Mode: {0}", MaskToString(TransportModes.DefaultMode));

            return TransportModeMasks;
        }

        private static void SetTransportModeNames(string[] transportModes)
        {
            try
            {
                Array.Resize(ref TransportModeNames, transportModes.Length);
                for(int i = 0; i < transportModes.Length; i++)
                {
                    TransportModeNames[i] = transportModes[i];
                }
            }
            catch (Exception e)
            {
                logger.Info("Initialization error in the creation of the Transport Mode List: {0}", e.Message);
            }
        }

        public static void SetPublicTransportModes(string[] publicTransportModes)
        {
            PublicTransportModes = TransportModes.ArrayToMask(TransportModes.NameSequenceToMasksArray(publicTransportModes));
        }

        public static void SetPublicTransportModes(byte publicTransportModes)
        {
            PublicTransportModes = publicTransportModes;
        }

        public static void LoadTransportModeRoutingRules(TransportModeRoutingRule[] transportModeRoutingRules)
        {
            RoutingRules = TransportModes.CreateTransportModeRoutingRules(transportModeRoutingRules);
        }

        private static Dictionary<byte,byte> CreateTransportModeRoutingRules(TransportModeRoutingRule[] transportModeRoutingRules)
        {
            Dictionary<byte,byte> transportModeRoutingRoules = new Dictionary<byte,byte>();

            for(var i = 0; i < transportModeRoutingRules .Length; i++)
            {
                byte currentTransportMode = GetMaskFromNames(new string[1] {(transportModeRoutingRules[i].CurrentTransportMode)});
                byte[] alternativeTransportModes = NameSequenceToMasksArray(transportModeRoutingRules[i].AlternativeTransportModes);
                byte alternativeTransportModesMask = ArrayToMask(alternativeTransportModes);
                
                if (!transportModeRoutingRoules.ContainsKey(currentTransportMode))
                {
                    transportModeRoutingRoules.Add(currentTransportMode, alternativeTransportModesMask);
                }
                else
                {
                    logger.Debug("Unable to add Transport Mode routing rule. Transport Mode: {0}", transportModeRoutingRules[i].CurrentTransportMode);
                }
            }
            
            return transportModeRoutingRoules;
        }

        public static async Task CreateMappingTagIdToTransportModes()
        {
            OSMTagIdToTransportModes = await TransportModes.CreateMappingTagIdToTransportModes(TransportModeMasks);
        }

        private static async Task<Dictionary<int,byte>> CreateMappingTagIdToTransportModes(Dictionary<int,byte> transportModeMasks)
        {
            int[] configTagIds = await Configuration.ValidateOSMTags();

            Dictionary<int,byte> tagIdToTransportModes = new Dictionary<int,byte>();

            for(var i = 0; i < configTagIds.Length; i++)
            {
                byte mask = 0; // Default Transport Mode: 0

                var configAllowedTransportModes = Configuration.ValidateAllowedTransportModes(Configuration.OSMTagsToTransportModes[i].AllowedTransportModes);
                foreach(var transportName in configAllowedTransportModes)
                {
                    int transportModeIndex = GetTransportModeNameIndex(transportName);
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

        public static Dictionary<int,byte> CreateMappingRouteTypeToTransportMode(Dictionary<int,byte> transportModeMasks){
            Dictionary<int,byte> routeTypeToTransportMode= Configuration.CreateMappingTypeRouteToTransportMode(transportModeMasks);
                        foreach(var rt2tmm in routeTypeToTransportMode)
            {
                logger.Info("{0}: {1} :: {2}", rt2tmm.Key,rt2tmm.Value,TransportModesToString(rt2tmm.Value));
            }
            return routeTypeToTransportMode;
        }

        public static int GetTransportModeNameIndex(string transportModeName)
        {
            for(int i = 1; i < TransportModeNames.Length; i++)
            {
                if(TransportModeNames[i].Equals(transportModeName))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}