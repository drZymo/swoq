namespace Swoq.Infra;

public static class Clock
{
    public static void Reset()
    {
        now = null;
    }

    public static void Setup(Func<DateTime> now)
    {
        Clock.now = now;
    }

    private static Func<DateTime>? now = null;

    public static DateTime Now => now?.Invoke() ?? DateTime.Now;
}
