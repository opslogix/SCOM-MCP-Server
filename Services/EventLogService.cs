using System;
using System.Diagnostics;
using System.IO;

namespace SCOMMCPServer.Services
{
    public class EventLogService
    {
        private readonly string _eventSource = "SCOM MCP Server";
        private readonly string _eventLogName = "Operations Manager";
        private bool _sourceExists = false;
        private readonly TextWriter _console;

        // Event ID Categories
        public const int EVENT_ID_STARTUP = 1000;
        public const int EVENT_ID_QUERY = 1001;
        public const int EVENT_ID_UPDATE = 1002;
        public const int EVENT_ID_MAINTENANCE = 1003;
        public const int EVENT_ID_ERROR = 3000;
        public const int EVENT_ID_WARNING = 2000;

        public EventLogService()
        {
            _console = Console.Error;
            InitializeEventSource();
        }

        private void InitializeEventSource()
        {
            try
            {
                if (!EventLog.SourceExists(_eventSource))
                {
                    EventLog.CreateEventSource(_eventSource, _eventLogName);
                    WriteToConsole($"Created event source: {_eventSource}");
                }
                _sourceExists = true;
            }
            catch (Exception ex)
            {
                WriteToConsole($"Unable to create event source, will use fallback logging: {ex.Message}");
                _sourceExists = false;
            }
        }

        public void LogInformation(string message, int eventId = EVENT_ID_STARTUP)
        {
            WriteEntry(message, EventLogEntryType.Information, eventId);
        }

        public void LogQuery(string operation, int resultCount, long milliseconds)
        {
            string message = $"{operation} executed successfully, returned {resultCount} results in {milliseconds}ms";
            WriteEntry(message, EventLogEntryType.Information, EVENT_ID_QUERY);
        }

        public void LogWarning(string message, int eventId = EVENT_ID_WARNING)
        {
            WriteEntry(message, EventLogEntryType.Warning, eventId);
        }

        public void LogError(string message, Exception exception = null, int eventId = EVENT_ID_ERROR)
        {
            string fullMessage = exception != null
                ? $"{message}\nException: {exception}"
                : message;

            WriteEntry(fullMessage, EventLogEntryType.Error, eventId);
        }

        private void WriteToConsole(string message)
        {
            _console.WriteLine(message);
            _console.Flush();
        }

        private void WriteEntry(string message, EventLogEntryType entryType, int eventId)
        {
            // Format and write to console
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string consoleMessage = $"[{timestamp}] [{entryType}] [EventID:{eventId}] {message}";

            // Direct write to stderr with immediate flush
            WriteToConsole(consoleMessage);

            // Also write to Windows Event Log if available
            if (_sourceExists)
            {
                try
                {
                    EventLog.WriteEntry(_eventSource, message, entryType, eventId);
                }
                catch (Exception ex)
                {
                    WriteToConsole($"[{timestamp}] [Warning] Failed to write to Windows Event Log: {ex.Message}");
                }
            }
        }
    }
}