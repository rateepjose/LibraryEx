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

        public TimeSpan PollFrequency { get; set; } = TimeSpan.FromMilliseconds(50);

        #region Constructor and Destructors
        public ActiveObjectPart(string partName)
        {
            WorkQueue = new CmdQueue();
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
                Thread.Sleep(PollFrequency);
                try
                {
                    RunQueuedCommandsIfAny();
                    OnService();
                }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex.Message); }
            }
        }

        private void RunQueuedCommandsIfAny()
        {
            if (WorkQueue.Empty) { return; }
            ICommand cmd;
            WorkQueue.Pop(out cmd);
            cmd.RunCmdFunc();
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

        public ICommandProxy CreateCommand(string cmdName, Func<string> func) => new Command(cmdName, func, WorkQueue);


        #region Command class
        private class Command : ICommand
        {
            #region CommandStatus
            public bool IsComplete => _cmdExectutionStatus == CommandExecutionStatus.Completed;

            public bool IsProcessing => _cmdExectutionStatus == CommandExecutionStatus.Processing;

            public bool IsQueued => _cmdExectutionStatus == CommandExecutionStatus.Queued;

            private string _errorCode;
            public string ErrorCode { get { return _errorCode; } }

            private ManualResetEventSlim _commandCompletedEvent = new ManualResetEventSlim();
            public string WaitForCompletion()
            {
                if (_cmdExectutionStatus == CommandExecutionStatus.Completed) { return ErrorCode; }
                try { _commandCompletedEvent.Wait(); } catch { }
                return ErrorCode;
            }
            #endregion

            #region CommandProxy
            public string Run() => PackAndEnqueue().WaitForCompletion();

            public ICommandStatus Start() => PackAndEnqueue();

            private ICommandStatus PackAndEnqueue()
            {
                CommandExecutionStatus ces = _cmdExectutionStatus;
                if (!(ces == CommandExecutionStatus.Completed
                    || ces == CommandExecutionStatus.NotQueued)) { return this; }

                _commandCompletedEvent.Reset();
                _cmdExectutionStatus = CommandExecutionStatus.Queued;
                return _queue.Push(this);
            }
            #endregion

            public string CmdName { get; private set; }
            public Func<string> CmdFunc { get; private set; }
            private volatile CommandExecutionStatus _cmdExectutionStatus = CommandExecutionStatus.NotQueued;
            private IWorkQueue _queue;
            public Command(string cmdName, Func<string> func, IWorkQueue queue) { CmdName = cmdName; CmdFunc = func; _queue = queue; }

            public void RunCmdFunc()
            {
                _errorCode = string.Empty;
                _cmdExectutionStatus = CommandExecutionStatus.Processing;
                try { _errorCode = CmdFunc(); }
                catch(Exception ex) { _errorCode = ex.Message; }
                finally
                {
                    _cmdExectutionStatus = CommandExecutionStatus.Completed;
                    _commandCompletedEvent.Set();
                }
            }
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
    }

    public interface ICommandProxy
    {
        string Run();
        ICommandStatus Start();
    }

    public interface ICommandInternal
    {
        string CmdName { get; }
        Func<string> CmdFunc { get; }
        void RunCmdFunc();
    }

    public interface ICommand : ICommandProxy, ICommandStatus, ICommandInternal { }


    public interface IWorkQueue
    {
        bool Empty { get; }
        ICommandStatus Push(ICommand command);
        string Pop(out ICommand command);
    }

    public class CmdQueue : IWorkQueue
    {
        private object _lock;
        private Queue<ICommand> _queue;

        public CmdQueue()
        {
            _lock = new object();
            _queue = new Queue<ICommand>();
        }

        public bool Empty { get { lock (_lock) { return _queue.Count == 0; } } }

        public string Pop(out ICommand command)
        {
            command = null;
            lock (_lock) { try { command = _queue.Dequeue(); } catch (Exception ex) { return ex.Message; } }
            return string.Empty;
        }

        public ICommandStatus Push(ICommand command) { lock (_lock) { _queue.Enqueue(command); } return command; }
    }

    public static class Extensions
    {
        public static bool IsNullOrEmpty(this string str) { return string.IsNullOrEmpty(str); }
        public static string FormatEx(this string str, params object[] args) { return string.Format(str, args); }
    }
}
