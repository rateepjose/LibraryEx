using LibraryEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.ComponentModel;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ActiveObjectPart _aop;
        DispatcherTimer _dt;
        public System.Windows.Input.ICommand TestWpfCmd { get; set; } = new TestWpfCommand();
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _testData = new RefObjectObserver<Data>(_test);
            _aop = new ActiveObjectPart("test", TimeSpan.FromMilliseconds(100)) { ServiceFunc = RunningIndicatorFunc, };
            string ec = _aop.Initialize();
            _dt = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(333) };
            _dt.Tick += UI_Update;
            _dt.Start();
            int worker, completion;
            System.Threading.ThreadPool.GetMaxThreads(out worker, out completion);
        }

        private char[] _runningIndicatorArray = { '|', '/', '-', '\\', };
        private int _runningIndicatorIndex = 0;
        //private void RunningIndicatorFunc() => _runningIndicatorIndex = _runningIndicatorIndex >= 3 ? 0 : ++_runningIndicatorIndex;

        private class Data { public int Index { get; set; } }
        private RefObjectPublisher<Data> _test = new RefObjectPublisher<Data>() { Object = new Data() { Index = 0, } };
        private void RunningIndicatorFunc()
        {
            _runningIndicatorIndex = _runningIndicatorIndex >= 3 ? 0 : ++_runningIndicatorIndex;
            _test.Object = new Data() { Index = _runningIndicatorIndex };
        }

        private List<ICommandStatus> _commands = new List<ICommandStatus>(100);
        private RefObjectObserver<Data> _testData;
        private void UI_Update(object sender, EventArgs e)
        {
            _data.Text = _multiCommandResult;
            _dataGrid.ItemsSource = _commands.ToArray();
            //_mainWindow.Title = "MainWindow {0}".FormatEx(_runningIndicatorArray[_runningIndicatorIndex]);
            bool chumma;
            _mainWindow.Title = "MainWindow {0}".FormatEx(_runningIndicatorArray[_testData.Update(out chumma).Object.Index]);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _aop?.Dispose();
            _aop = null;
        }

        private int _index = 0;
        ICommandProxy icp;
        private void Test_Click(object sender, RoutedEventArgs e)
        {
            int i = ++_index;
            _dataInit.Text = _index.ToString();
            if (icp == null) { icp = _aop.CreateCommand("MULTICOMMAND", (x) => RunCommand(x, _index)); _commands.Add(icp.Start()); }
            else { icp.Start(); }
            //MessageBox.Show(_aop.CreateCommand("Multi", () => RunCommand(_index)).Run());
            System.Diagnostics.Trace.WriteLine("Test_Click{0}".FormatEx(i));
        }

        public string _multiCommandResult { get; set; } = "Empty";
        private string RunCommand(ICommandParams cmdParams, int i)
        {
            for (long j = 0; j < 2000; ++j)
                for (long k = 0; k < 500000; ++k) { }
            _multiCommandResult =  "MultiCommand: Index ={0}".FormatEx(i);
            System.Diagnostics.Trace.WriteLine(_multiCommandResult);

            cmdParams.SetOutputParams(new OutputParams1() { Value = i });
            return i % 2 != 0 ? _multiCommandResult : string.Empty;
        }

        public class OutputParams1 : IOutputParams { public int Value { get; set; } }

        private int _indexMulti = 0;
        private void Test_ClickMulti(object sender, RoutedEventArgs e)
        {
            int i = ++_indexMulti;
            _dataInit.Text = _indexMulti.ToString();
            _commands.Add(_aop.CreateCommand("MULTICOMMAND", (x) => RunCommand(x, i)).Start());
            System.Diagnostics.Trace.WriteLine("Test_Click{0}".FormatEx(i));
        }

    }

    public class TestWpfCommand : System.Windows.Input.ICommand, INotifyPropertyChanged
    {
        public event EventHandler CanExecuteChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        //{
        //    add { CommandManager.RequerySuggested += value; }
        //    remove { CommandManager.RequerySuggested -= value; }
        //}

        public bool CanExecute(object parameter) => CannotExecuteReason.IsNullOrEmpty();//_flag;
        ////private bool _flag = true;
        public async void Execute(object parameter)
        {
            CannotExecuteReason = "Executing";////_flag = false;
            //CommandManager.InvalidateRequerySuggested();
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            System.Diagnostics.Trace.WriteLine("Enter{0}".FormatEx(System.Threading.Thread.CurrentThread.ManagedThreadId));
            await Task.Delay(5000);
            System.Diagnostics.Trace.WriteLine("Exit{0}".FormatEx(System.Threading.Thread.CurrentThread.ManagedThreadId));
            CannotExecuteReason = string.Empty;////_flag = true;
            CanExecuteChanged?.Invoke(this, new EventArgs());
            //CommandManager.InvalidateRequerySuggested();
        }
        private string _cannotExecuteReason = string.Empty;
        public string CannotExecuteReason { get => _cannotExecuteReason; private set { if (value != _cannotExecuteReason) { _cannotExecuteReason = value; NotifyPropertyChanged(); ToolTipEnabled = !_cannotExecuteReason.IsNullOrEmpty(); ; } } }

        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool _toolTipEnabled = false;
        public bool ToolTipEnabled { get => _toolTipEnabled; set { if (value != _toolTipEnabled) { _toolTipEnabled = value; NotifyPropertyChanged(); } } }
    }
}
