namespace SytyRouting
{
    public class Helper
    {
        public static string FormatElapsedTime(TimeSpan timeSpan)
        {
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
                timeSpan.Milliseconds);

            return elapsedTime;
        }
    }
}