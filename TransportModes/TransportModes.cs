using NLog;

namespace SytyRouting
{
    public static class TransportModes
    {
        public const string DefaulTransportMode = "None";
        public const int MaxNumberOfTransportModes = sizeof(byte) * 8; // Number of bits to be used in the TransportModes masks

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static Dictionary<int,byte> TransportModeMasks = new Dictionary<int,byte>();
        private static Dictionary<int,byte> OSMTagIdToTransportModes = new Dictionary<int,byte>();

        
        public static byte GetTransportModesMask(string[] transportModeNames)
        {
            byte transportModesMask = 0;
            for(int i = 0; i < transportModeNames.Length; i++)
            {
                int transportModeIndex = TransportModes.GetTransportModeIndex(transportModeNames[i]);
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
                    result += Configuration.TransportModeNames[transportModeMask.Key] + ", ";
                }
            }
                
            return (result == "")? TransportModes.DefaulTransportMode : result;
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
                
            return result.ToArray(); // == "")? TransportModes.DefaulTransportMode : result;
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
                return (byte)0; // Default Ttransport Mode: 0 ("None");
            }
        }

        public static Dictionary<int,byte> CreateTransportModeMasks(string[] transportModes)
        {
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
                    int transportModeIndex = GetTransportModeIndex(transportName);
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

        private static int GetTransportModeIndex(string transportModeName)
        {
            int index = 0;
            for(int i = 1; i < Configuration.TransportModeNames.Length; i++)
            {
                if(Configuration.TransportModeNames[i].Equals(transportModeName))
                {
                    index = i;
                    break;
                }
            }

            return index;
        }
    }
}