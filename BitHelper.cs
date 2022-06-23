namespace SytyRouting
{
    public static class BitHelper
    {
        public static double ReadDouble(byte[] bytes, ref int pos)
        {
            var result = BitConverter.ToDouble(bytes, pos);
            pos += 8;
            return result;
        }

        public static bool ReadBoolean(byte[] bytes, ref int pos)
        {
            var result = BitConverter.ToBoolean(bytes, pos);
            pos += 1;
            return result;
        }

        public static int ReadInt32(byte[] bytes, ref int pos)
        {
            var result = BitConverter.ToInt32(bytes, pos);
            pos += 4;
            return result;
        }

        public static long ReadInt64(byte[] bytes, ref int pos)
        {
            var result = BitConverter.ToInt64(bytes, pos);
            pos += 8;
            return result;
        }
    }
}