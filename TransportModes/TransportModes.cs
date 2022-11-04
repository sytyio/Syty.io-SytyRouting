using NLog;

namespace SytyRouting
{
    public static class TransportModes
    {
        public const string NoTransportMode = "None";
        public const int MaxNumberOfTransportModes = sizeof(byte) * 8; // Number of bits to be used in the TransportModes masks

        private static string[] TransportModeNames = new string[1] {NoTransportMode};
        private static Dictionary<int,byte> TransportModeMasks = new Dictionary<int,byte>();
        private static Dictionary<int,byte> OSMTagIdToTransportModes = new Dictionary<int,byte>();

        
        private static Logger logger = LogManager.GetCurrentClassLogger();
        

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

        public static string NamesToString(string[] transportModeNames)
        {
            string transportModeNamesString = "";
            for(int i = 0; i < transportModeNames.Length-1; i++)
            {
                transportModeNamesString += transportModeNames[i] + ", ";
            }
            transportModeNamesString += transportModeNames[transportModeNames.Length-1];
            
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
                    logger.Info("Transport Mode Sequence: Transport Mode '{0}' not found.", transportModesSequence[i]);
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

            return TransportModeMasks;
        }

        public static async Task CreateMappingTagIdToTransportModes()
        {
            OSMTagIdToTransportModes = await TransportModes.CreateMappingTagIdToTransportModes(TransportModeMasks);
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

        private static int GetTransportModeNameIndex(string transportModeName)
        {
            int index = 0;
            for(int i = 1; i < TransportModeNames.Length; i++)
            {
                if(TransportModeNames[i].Equals(transportModeName))
                {
                    index = i;
                    break;
                }
            }

            return index;
        }
    }
}