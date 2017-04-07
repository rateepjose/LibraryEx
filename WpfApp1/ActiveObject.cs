using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class ActiveObjectPart : IDisposable
    {
        private string _name;
        private Thread _activeObjectThread;
        private bool _initialized;
        public IWorkQueue WorkQueue { get; private set; }

        #region Constructor and Destructors

        public ActiveObjectPart(string partName, TimeSpan? waitTime = null)
        {
            WorkQueue = new CmdQueue() { WaitTime = waitTime ?? TimeSpan.FromMilliseconds(50) };
            _initialized = false;
            _name = partName;
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
                    OnService();
                }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex.Message); }
            }
        }

        private void RunOneQueuedCommand()
        {
            ICommand cmd;
            if (WorkQueue.Pop(out cmd).IsNullOrEmpty()) { cmd.RunCmdFunc(); }
        }

        protected virtual void OnService() { }

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
                if (_initialized) { ec = "The part=['{0}'] is already initialized".FormatEx(_name); return ec; }

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

        public ICommandProxy CreateCommand(string cmdName, Func<ICommandParams, string> func) => new Command(cmdName, func, WorkQueue);


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
            #endregion

            #region ICommandProxy
            public string Run() => PackAndEnqueue().WaitForCompletion();

            public ICommandStatus Start() => PackAndEnqueue();

            private ICommandStatus PackAndEnqueue()
            {
                CommandExecutionStatus ces = _cmdExecutionStatus;
                if (!(ces == CommandExecutionStatus.Completed
                    || ces == CommandExecutionStatus.NotQueued)) { return this; }

                _commandCompletedEvent.Reset();
                _cmdExecutionStatus = CommandExecutionStatus.Queued;
                return _queue.Push(this);
            }
            #endregion

            #region ICommandInternal

            public string CmdName { get; private set; }
            public Func<ICommandParams, string> CmdFunc { get; private set; }
            public void RunCmdFunc()
            {
                _outputParams = null;
                _errorCode = string.Empty;
                _cmdExecutionStatus = CommandExecutionStatus.Processing;
                try { _errorCode = CmdFunc(this); }
                catch(Exception ex) { _errorCode = ex.Message; }
                finally
                {
                    _cmdExecutionStatus = CommandExecutionStatus.Completed;
                    _commandCompletedEvent.Set();
                }
            }

            #endregion

            #region ICommandParams

            public void SetOutputParams(IOutputParams outputParams) => _outputParams = outputParams;

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

            public Command(string cmdName, Func<ICommandParams, string> func, IWorkQueue queue) { CmdName = cmdName; CmdFunc = func; _queue = queue; }

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
    }

    public interface ICommandStatus
    {
        bool IsComplete { get; }
        bool IsProcessing { get; }
        bool IsQueued { get; }
        string ErrorCode { get; }
        string WaitForCompletion();
        IOutputParams OutputParams { get; }
    }

    public interface ICommandProxy
    {
        string Run();
        ICommandStatus Start();
    }

    public interface ICommandInternal
    {
        string CmdName { get; }
        Func<ICommandParams, string> CmdFunc { get; }
        void RunCmdFunc();
    }

    public interface IOutputParams { }

    public interface ICommandParams
    {
        void SetOutputParams(IOutputParams outputParams);
    }

    public interface ICommand : ICommandProxy, ICommandStatus, ICommandInternal, ICommandParams, IDisposable { }


    public interface IWorkQueue
    {
        bool Empty { get; }
        ICommandStatus Push(ICommand command);
        string Pop(out ICommand command);
    }

    public class CmdQueue : IWorkQueue
    {
        private Queue<ICommand> _queue;

        public CmdQueue()
        {
            _queue = new Queue<ICommand>();
        }

        public TimeSpan WaitTime { get; set; }

        public bool Empty { get { lock (_queue) { return _queue.Count == 0; } } }

        public string Pop(out ICommand command)
        {
            command = null;
            lock (_queue)
            {
                try
                {
                    if (0 == _queue.Count) { if (!Monitor.Wait(_queue, WaitTime)) { return "Queue is empty after wait"; } }
                    command = _queue.Dequeue();
                }
                catch (Exception ex) { return ex.Message; }
            }
            return string.Empty;
        }

        public ICommandStatus Push(ICommand command) { lock (_queue) { _queue.Enqueue(command); if (1 == _queue.Count) { Monitor.Pulse(_queue); } } return command; }
    }

    public static class Extensions
    {
        public static bool IsNullOrEmpty(this string str) { return string.IsNullOrEmpty(str); }
        public static string FormatEx(this string str, params object[] args) { return string.Format(str, args); }
    }
}
