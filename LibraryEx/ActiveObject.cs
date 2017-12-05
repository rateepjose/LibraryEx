using System;
using System.Collections.Generic;
using System.Threading;

namespace LibraryEx
{
    public class ActiveObjectPart : IDisposable
    {
        private Thread _activeObjectThread;
        private bool _initialized;
        public IWorkQueue WorkQueue { get; private set; }
        public string Name { get; private set; }

        #region Constructor and Destructors

        public ActiveObjectPart(string partName, TimeSpan? waitTime = null)
        {
            WorkQueue = new CmdQueue() { WaitTime = waitTime ?? TimeSpan.FromMilliseconds(100) };
            _initialized = false;
            Name = partName;
        }

        ~ActiveObjectPart() { Dispose(false); }

        private bool _disposed = false;
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) { return; }

            if (disposing) { Uninitialize(); }

            _disposed = true;
        }

        #endregion

        private volatile bool _runThread;
        private void AOThread()
        {
            while (_runThread)
            {
                try
                {
                    RunOneQueuedCommand();
                    ServiceFunc?.Invoke();
                    OnService();
                }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex.Message); }
            }
        }

        private void RunOneQueuedCommand() { if (WorkQueue.Pull(out var cmd).IsNullOrEmpty()) { cmd.RunCmdFunc(); } }

        protected virtual void OnService() { }

        public Action ServiceFunc { get; set; } = null;

        public string Start()
        {
            try { _activeObjectThread.Start(); } catch (Exception ex) { return ex.Message; }
            return string.Empty;
        }

        public string Initialize(bool autoStart = true)
        {
            string ec = string.Empty;
            try
            {
                if (_initialized) { ec = "The part=['{0}'] is already initialized".FormatEx(Name); return ec; }

                _activeObjectThread = new Thread(() => AOThread()) { IsBackground = true, };
                _runThread = true;
                if (autoStart) { ec = Start(); }
                return string.Empty;
            }
            catch (Exception ex) { ec = ex.Message; return ec; }
            finally { _initialized = ec.IsNullOrEmpty(); }
        }

        private string Uninitialize()
        {
            _runThread = false;
            try
            {
                if (_activeObjectThread?.IsAlive == true)
                {
                    _activeObjectThread.Join(TimeSpan.FromMilliseconds(1000));
                    if (_activeObjectThread.IsAlive) { _activeObjectThread.Abort(); }
                }
            }
            catch { }
            finally { _activeObjectThread = null; }
            return string.Empty;
        }

        public ICommandProxy CreateCommand(string cmdName, Func<ICommandInteraction, string> func) => new Command(cmdName, func, WorkQueue);


        #region Command class
        private class Command : ICommand
        {
            #region ICommandStatus
            public bool IsComplete => _cmdExecutionStatus == CommandExecutionStatus.Completed;

            public bool IsProcessing => _cmdExecutionStatus == CommandExecutionStatus.Processing;

            public bool IsQueued => _cmdExecutionStatus == CommandExecutionStatus.Queued;

            private string _errorCode;
            public string ErrorCode { get { return _errorCode; } }

            private ManualResetEventSlim _commandCompletedEvent = new ManualResetEventSlim();
            public string WaitForCompletion()
            {
                if (_cmdExecutionStatus == CommandExecutionStatus.Completed) { return ErrorCode; }
                try { _commandCompletedEvent.Wait(); } catch { }
                return ErrorCode;
            }

            private IOutputParams _outputParams;
            public IOutputParams OutputParams => _outputParams;

            private bool _isAbortRequested = false;
            public void Abort() => _isAbortRequested = true;
            public bool IsAborted => _cmdExecutionStatus == CommandExecutionStatus.Aborted;
            #endregion

            #region ICommandProxy
            public string Run() => PackAndEnqueue().WaitForCompletion();

            public ICommandStatus Start() => PackAndEnqueue();

            private ICommandStatus PackAndEnqueue()
            {
                //Reset the abort flag(required for cases where the same proxy is reused)
                _isAbortRequested = false;

                CommandExecutionStatus ces = _cmdExecutionStatus;
                if (!(ces == CommandExecutionStatus.Completed
                    || ces == CommandExecutionStatus.NotQueued
                    || ces == CommandExecutionStatus.Aborted)) { return this; }

                _commandCompletedEvent.Reset();
                _cmdExecutionStatus = CommandExecutionStatus.Queued;
                return _queue.Push(this);
            }
            #endregion

            #region ICommandInternal

            public string CmdName { get; private set; }
            public Func<ICommandInteraction, string> CmdFunc { get; private set; }
            public void RunCmdFunc()
            {
                _outputParams = null;
                _errorCode = string.Empty;
                _cmdExecutionStatus = CommandExecutionStatus.Processing;
                try { _errorCode = CmdFunc(this); }
                catch (Exception ex) { _errorCode = ex.Message; }
                finally
                {
                    _cmdExecutionStatus = _isAbortRequested ? CommandExecutionStatus.Aborted : CommandExecutionStatus.Completed;
                    _commandCompletedEvent.Set();
                }
            }
            public void ForceAbortIfNotYetProcessed()
            {
                _outputParams = null;
                _errorCode = "Aborted when queued";
                _cmdExecutionStatus = CommandExecutionStatus.Aborted;
                _commandCompletedEvent.Set();
            }

            #endregion

            #region ICommandInteraction

            public void SetOutputParams(IOutputParams outputParams) => _outputParams = outputParams;
            public bool IsAbortRequested => _isAbortRequested;

            #endregion

            #region IDisposable

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private bool _disposed = false;
            protected virtual void Dispose(bool disposing)
            {
                if (_disposed) { return; }

                if (disposing) { _commandCompletedEvent.Dispose(); }

                _disposed = true;
            }

            #endregion

            private volatile CommandExecutionStatus _cmdExecutionStatus = CommandExecutionStatus.NotQueued;
            private IWorkQueue _queue;

            #region Constructor and Destructors

            public Command(string cmdName, Func<ICommandInteraction, string> func, IWorkQueue queue) { CmdName = cmdName; CmdFunc = func; _queue = queue; }

            #endregion
        }
        #endregion
    }


    public enum CommandExecutionStatus : int
    {
        NotQueued,
        Queued,
        Processing,
        Completed,
        Aborted,
    }

    public interface ICommandStatus
    {
        bool IsComplete { get; }
        bool IsProcessing { get; }
        bool IsQueued { get; }
        string ErrorCode { get; }
        string WaitForCompletion();
        IOutputParams OutputParams { get; }
        void Abort();
        bool IsAborted { get; }
    }

    public interface ICommandProxy
    {
        string Run();
        ICommandStatus Start();
    }

    public interface ICommandInternal
    {
        string CmdName { get; }
        Func<ICommandInteraction, string> CmdFunc { get; }
        void RunCmdFunc();
        void ForceAbortIfNotYetProcessed();
    }

    public interface IOutputParams { }

    public interface ICommandInteraction
    {
        void SetOutputParams(IOutputParams outputParams);
        bool IsAbortRequested { get; }
    }

    public interface ICommand : ICommandProxy, ICommandStatus, ICommandInternal, ICommandInteraction, IDisposable { }


    public interface IWorkQueue
    {
        bool Empty { get; }
        ICommandStatus Push(ICommand command);
        string Pull(out ICommand command);
    }

    public class CmdQueue : IWorkQueue
    {
        private List<ICommand> _queue;

        private readonly object _lockObj = new object();

        public CmdQueue() => _queue = new List<ICommand>();

        public TimeSpan WaitTime { get; set; }

        public bool Empty { get { lock (_lockObj) { return _queue.Count == 0; } } }

        private void RemoveAbortedCommandsFromQueue()
        {
            List<ICommand> queue = new List<ICommand>();
            foreach (var item in _queue)
            {
                if (item.IsAbortRequested) { item.ForceAbortIfNotYetProcessed(); continue; }
                queue.Add(item);
            }
            _queue = queue;
        }

        public string Pull(out ICommand command)
        {
            command = null;
            lock (_lockObj)
            {
                try
                {
                    RemoveAbortedCommandsFromQueue();
                    if (0 == _queue.Count) { if (!Monitor.Wait(_lockObj, WaitTime)) { return "Queue is empty after wait"; } }
                    command = _queue[0];
                    _queue.RemoveAt(0);
                }
                catch (Exception ex) { return ex.Message; }
            }
            return string.Empty;
        }

        public ICommandStatus Push(ICommand command) { lock (_lockObj) { _queue.Add(command); if (1 == _queue.Count) { Monitor.Pulse(_lockObj); } } return command; }
    }

    public static partial class Extensions
    {
        public static bool IsNullOrEmpty(this string str) { return string.IsNullOrEmpty(str); }
        public static string FormatEx(this string str, params object[] args) { return string.Format(str, args); }
    }
}
