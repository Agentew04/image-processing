using System.Diagnostics;
using System.Text.Json;

namespace Capture;

public class TraceEvent
{
    public string name { get; set; }
    public string cat { get; set; }
    public string ph { get; set; } // B/E
    public long ts { get; set; }   // timestamp (microseconds)
    public int pid { get; set; } = 1;
    public int tid { get; set; }
}

public class ChromeTraceSession
{
    private readonly List<TraceEvent> _events = new();

    internal void AddEvent(TraceEvent e)
    {
        _events.Add(e);
    }

    public string ToJson(bool indented = false)
    {
        var wrapper = new
        {
            traceEvents = _events
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = indented
        };

        return JsonSerializer.Serialize(wrapper, options);
    }
}

public sealed class ChromeTraceScope : IDisposable
{
    private static readonly AsyncLocal<ChromeTraceSession> _session = new();

    private readonly string _name;
    private readonly string _category;
    private readonly int _threadId;
    private readonly long _startTs;

    public static void StartSession()
    {
        _session.Value = new ChromeTraceSession();
    }

    public static ChromeTraceSession EndSession()
    {
        var s = _session.Value;
        _session.Value = null;
        return s;
    }

    public ChromeTraceScope(string category, string name)
    {
        if (_session.Value == null)
            throw new InvalidOperationException("Call StartSession() first.");

        _name = name;
        _category = category;
        _threadId = Thread.CurrentThread.ManagedThreadId;

        _startTs = GetTimestampMicro();

        _session.Value.AddEvent(new TraceEvent
        {
            name = _name,
            cat = _category,
            ph = "B",
            ts = _startTs,
            tid = _threadId
        });
    }

    public void Dispose()
    {
        var endTs = GetTimestampMicro();

        _session.Value.AddEvent(new TraceEvent
        {
            name = _name,
            cat = _category,
            ph = "E",
            ts = endTs,
            tid = _threadId
        });
    }

    private static long GetTimestampMicro()
    {
        return (long)(Stopwatch.GetTimestamp() * 1_000_000.0 / Stopwatch.Frequency);
    }
}