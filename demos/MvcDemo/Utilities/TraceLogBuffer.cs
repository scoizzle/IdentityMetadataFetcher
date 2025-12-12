using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MvcDemo.Utilities
{
    /// <summary>
    /// Singleton circular buffer that captures trace logs from System.Diagnostics.
    /// </summary>
    public class TraceLogBuffer : TraceListener
    {
        private static readonly Lazy<TraceLogBuffer> _instance = new Lazy<TraceLogBuffer>(() => new TraceLogBuffer());
        
        private readonly Queue<TraceLogEntry> _buffer;
        private readonly int _maxCapacity;
        private readonly object _lockObject = new object();

        public static TraceLogBuffer Instance => _instance.Value;

        public int MaxCapacity => _maxCapacity;

        public int Count
        {
            get
            {
                lock (_lockObject)
                {
                    return _buffer.Count;
                }
            }
        }

        private TraceLogBuffer(int maxCapacity = 500)
        {
            _maxCapacity = maxCapacity;
            _buffer = new Queue<TraceLogEntry>(maxCapacity);
        }

        public override void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            AddEntry(message, TraceEventType.Information);
        }

        public override void WriteLine(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            AddEntry(message, TraceEventType.Information);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var formattedMessage = args != null && args.Length > 0 
                ? string.Format(message, args) 
                : message;

            AddEntry($"[{source}] {formattedMessage}", eventType);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            TraceEvent(eventCache, source, eventType, id, "");
        }

        private void AddEntry(string message, TraceEventType eventType)
        {
            lock (_lockObject)
            {
                if (_buffer.Count >= _maxCapacity)
                {
                    _buffer.Dequeue();
                }

                _buffer.Enqueue(new TraceLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Message = message,
                    Level = eventType.ToString()
                });
            }
        }

        public List<TraceLogEntry> GetLogs()
        {
            lock (_lockObject)
            {
                return _buffer.ToList();
            }
        }

        public List<TraceLogEntry> GetLogsSince(DateTime timestamp)
        {
            lock (_lockObject)
            {
                return _buffer.Where(log => log.Timestamp >= timestamp).ToList();
            }
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _buffer.Clear();
            }
        }
    }

    public class TraceLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public string Level { get; set; }

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }
}
