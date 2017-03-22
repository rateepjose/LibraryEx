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

        public TimeSpan PollFrequency { get; set; } = TimeSpan.FromMilliseconds(300);

        #region Constructor and Destructors
        public ActiveObjectPart(string partName)
        {
            _initialized = false;
            _name = partName;
            _activeObjectThread = new Thread(() => AOThread()) { IsBackground = true, };
        }

        ~ActiveObjectPart() { Dispose(false); }

        private bool _disposed = false;
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) { return; }

            if (disposing){ Uninitialize(); }

            _disposed = true;
        }


        #endregion

        private volatile bool _runThread;
        private void AOThread()
        {
            while (_runThread)
            {
                Thread.Sleep(PollFrequency);
                OnPoll();
            }
        }

        protected virtual void OnPoll() { }

        public string Initialize()
        {
            string ec = string.Empty;
            try
            {
                if (_initialized) { ec = "The part=['{0}'] is already initialized".format(_name); return ec; }

                _runThread = true;
                _activeObjectThread.Start();
                return string.Empty;
            }
            catch (Exception ex) { ec = ex.Message; return ec; }
            finally { _initialized = ec.IsNullOrEmpty(); }
        }

        private string Uninitialize()
        {
            _runThread = false;
            try { _activeObjectThread?.Join(); } catch { }
            return string.Empty;
        }
    }

    public static class Extensions
    {
        public static bool IsNullOrEmpty(this string str) { return string.IsNullOrEmpty(str); }
        public static string format(this string str, params object[] args) { return string.Format(str, args); }
    }
}
