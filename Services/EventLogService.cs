using System;
using System.Diagnostics;

namespace SCOMMCPServer.Services
{
    public class EventLogService
    {
        private readonly string _eventSource = "SCOM MCP Server";
        private readonly string _eventLogName = "Operations Manager";
        private bool _sourceExists = false;

        public EventLogService()
        {
            InitializeEventSource();
        }

        private void InitializeEventSource()
        {
            try
            {
                // Check if the event source exists
                if (!EventLog.SourceExists(_eventSource))
                {
                    // Create event source (requires admin privileges)
                    EventLog.CreateEventSource(_eventSource, _eventLogName);
                    Console.Error.WriteLine($"Created event source: {_eventSource}");
                }
                _sourceExists = true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unable to create event source, will use fallback logging: {ex.Message}");
                _sourceExists = false;
            }
        }

        public void LogInformation(string message, int eventId = 1000)
        {
            WriteEntry(message, EventLogEntryType.Information, eventId);
        }

        public void LogWarning(string message, int eventId = 2000)
        {
            WriteEntry(message, EventLogEntryType.Warning, eventId);
        }

        public void LogError(string message, Exception exception = null, int eventId = 3000)
        {
            string fullMessage = exception != null
                ? $"{message}\nException: {exception}"
                : message;

            WriteEntry(fullMessage, EventLogEntryType.Error, eventId);
        }

        private void WriteEntry(string message, EventLogEntryType entryType, int eventId)
        {
            try
            {
                if (_sourceExists)
                {
                    EventLog.WriteEntry(_eventSource, message, entryType, eventId);
                    Console.Error.WriteLine($"Event log entry written: {message}");
                }
                else
                {
                    // Fallback to console logging
                    Console.Error.WriteLine($"EventLog [{entryType}] {message}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write to event log: {ex.Message}");
            }
        }
    }
}