public class TimeHelpers
{
    public static double Now()
    {
        return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }

    public static string TimeSince(double start)
    {
        return FormatMs(Now() - start);
    }

    public static string FormatMs(double ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}