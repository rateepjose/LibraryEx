using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraryEx
{
    [Flags]
    public enum LogType: byte
    {
        None = 0x00,
        Debug = 0x01,
        Info = 0x02,
        Warning = 0x04,
        Error = 0x08,
        All = 0xFF,
    }
    public interface ILogClient
    {
        void WriteLog(DateTime dateTime, LogType logType, string message);
    }
    public static class Logger
    {
        public class MessageLogger
        {
            Action<string> _func;
            public MessageLogger(Action<string> func) => _func = func;
            public void WriteLine(string message) => _func(message);
        }

        private static (ILogClient client, LogType filter)[] _logClients = new(ILogClient, LogType)[0];
        private static ActiveObjectPart _aop;
        static Logger()
        {
            _aop = new ActiveObjectPart("Logger");
            _aop.Initialize();
        }

        private static MessageLogger _debug = new MessageLogger(msg => WriteLine(LogType.Debug, msg));
        public static MessageLogger Debug => _debug;

        private static MessageLogger _info = new MessageLogger(msg => WriteLine(LogType.Info, msg));
        public static MessageLogger Info => _info;

        private static MessageLogger _error = new MessageLogger(msg => WriteLine(LogType.Error, msg));
        public static MessageLogger Error => _error;

        private static MessageLogger _warning = new MessageLogger(msg => WriteLine(LogType.Warning, msg));
        public static MessageLogger Warning => _warning;

        public static void RegisterClient(ILogClient client, LogType filter = LogType.All) => _aop.CreateCommand("RegisterClient", _ => PerformRegisterClient(client, filter)).Start();
        private static string PerformRegisterClient(ILogClient client, LogType filter)
        {
            if (null == client) { return "Invalid client(null object)"; }
            var clients = _logClients.ToList();
            clients.Add((client, filter));
            _logClients = clients.ToArray();
            return string.Empty;
        }

        private static void WriteLine(LogType logType, string message) => _aop.CreateCommand("WriteLine", _ => PerformWriteLine(DateTime.Now, logType, message)).Start();

        private static string PerformWriteLine(DateTime dateTime, LogType logType, string message)
        {
            foreach (var logClient in _logClients)
            {
                if (logType != (logClient.filter & logType)) { continue; }
                logClient.client.WriteLog(dateTime, logType, message);
            }
            return string.Empty;
        }
    }


    public class WindowsTraceClient : ILogClient
    {
        private ActiveObjectPart _aop;

        public WindowsTraceClient()
        {
            _aop = new ActiveObjectPart(nameof(WindowsTraceClient));
            _aop.Initialize();
        }

        #region ILogClient

        public void WriteLog(DateTime dateTime, LogType logType, string message) => _aop.CreateCommand("Log", _ => PerformLog(dateTime, logType, message)).Start();

        private string PerformLog(DateTime dateTime, LogType logType, string message)
        {
            System.Diagnostics.Trace.WriteLine($"{dateTime:yyyy/MM/dd HH:mm:ss} {logType}: {message}");
            return string.Empty;
        }

        #endregion
    }
}
