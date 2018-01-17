﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraryEx.Logging
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
    public interface IMessageLogger { void WriteLine(string message); }
    public static class Logger
    {
        private class MessageLogger :IMessageLogger
        {
            Action<string> _func;
            public MessageLogger(Action<string> func) => _func = func;
            public void WriteLine(string message) => _func(message);
        }

        private static (ILogClient client, LogType filter)[] _logClients = new(ILogClient, LogType)[0];
        private static ActiveObjectPart _aop;
        static Logger() => (_aop = new ActiveObjectPart("Logger")).Initialize();

        public static IMessageLogger Debug { get; private set; } = new MessageLogger(msg => WriteLine(LogType.Debug, msg));

        public static IMessageLogger Info { get; private set; } = new MessageLogger(msg => WriteLine(LogType.Info, msg));

        public static IMessageLogger Error { get; private set; } = new MessageLogger(msg => WriteLine(LogType.Error, msg));

        public static IMessageLogger Warning { get; private set; } = new MessageLogger(msg => WriteLine(LogType.Warning, msg));

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

    #region Log Clients

    public class WindowsTraceClient : ILogClient
    {
        private ActiveObjectPart _aop;

        public WindowsTraceClient() => (_aop = new ActiveObjectPart(nameof(WindowsTraceClient))).Initialize();

        #region ILogClient

        public void WriteLog(DateTime dateTime, LogType logType, string message) => _aop.CreateCommand("Log", _ => PerformLog(dateTime, logType, message)).Start();

        private string PerformLog(DateTime dateTime, LogType logType, string message)
        {
            System.Diagnostics.Trace.WriteLine($"{dateTime:yyyy/MM/dd HH:mm:ss} {logType}: {message}");
            return string.Empty;
        }

        #endregion

        #endregion
    }
}
