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
using LibraryEx;
using static LibraryEx.CommandDispatchManager;
using LibraryEx.Logging;

namespace WpfApp2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewHelper _vh;
        ModelObserverForVM _modelObserverForVM;
        SampleModel1 _sampleModel1;
        SampleModel2 _sampleModel2;
        CommandDispatchManager _cdm;
        public MainWindow()
        {
            Logger.RegisterClient(new WindowsTraceClient());
            Logger.Info.WriteLine("Logger is created!");
            //Create first a basic model for startup(optional step)
            _sampleModel2 = new SampleModel2();

            //Create ViewModel Observer
            _modelObserverForVM = new ModelObserverForVM();
            //Create ViewHelper that binds to VM and View. Default the view to poll every 333ms
            _vh = new ViewHelper(this, TimeSpan.FromMilliseconds(250)) { ViewModel = _modelObserverForVM, OnUiUpdate = UI_Update }.Initialize(ModelObserverCollection.Models);

            //Now create the remaining models

        }

        private void UI_Update()
        {
        }

        private void OnButton_CreateModels(object sender, RoutedEventArgs e)
        {
            Logger.Debug.WriteLine("OnButton_CreateModels Enter");
            //Create remaining Models
            _sampleModel1 = new SampleModel1();


            _cdm = new CommandDispatchManager(new ICommandDispatchClient[] { _sampleModel1 });
            _cdm.HarvestCommands().Run();
            _vh.Command = new TestCommand(_cdm);

            //for (int i = 0; i < 1000; ++i) Logger.Info.WriteLine($"{i}");

            _vh.Initialize(ModelObserverCollection.Models);
            Logger.Debug.WriteLine("OnButton_CreateModels Exit");
        }
    }

    #region Models
    public class SampleModel1 : ICommandDispatchClient
    {
        #region ICommandDispatchClient

        public string Name => _aop.Name;
        public Dictionary<string, (string[] reservations, string[] subCommands)> CommandToReservationsAndSubCommandsTable { get; private set; }
        public ICommandProxy StartCommand(string command, Dictionary<string, string> parameters, ICommandToken commandToken) => _aop.CreateCommand("StartCommand", _ => PerformStartCommand(command, parameters, commandToken));
        private string PerformStartCommand(string command, Dictionary<string, string> parameters, ICommandToken commandToken)
        {
            switch (command)
            {
                case "Reset": i = 0; Poll(); System.Threading.Thread.Sleep(1000); break;
                default: break;
            }
            return string.Empty;
        }
        #endregion

        public class CoordinateData
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        private ActiveObjectPart _aop;
        private RefObjectPublisher<CoordinateData> _data = new RefObjectPublisher<CoordinateData>() { Object = new CoordinateData { X = 0, Y = 0 } };
        public IRefObjectPublisher<CoordinateData> Data => _data;

        public SampleModel1()
        {
            _aop = new ActiveObjectPart("SampleModel1", TimeSpan.FromMilliseconds(100)) { ServiceFunc = Poll };
            ModelObserverCollection.AddModelObserverToCollection(new Dictionary<string, IRefObjectObserver>() { { $"{_aop.Name}.Data", new RefObjectObserver<CoordinateData>(Data) } });
            CommandToReservationsAndSubCommandsTable = new Dictionary<string, (string[] reservations, string[] subCommands)>() { { "Reset", (new string[] { $"{Name}", }, new string[] { }) }, };
            _aop.Initialize();
        }

        private int i = 0;
        private void Poll()
        {
            _data.Object = new CoordinateData() { X = ++i, Y = ++i };
        }
    }

    public class SampleModel2
    {

        private ActiveObjectPart _aop;
        private RefObjectPublisher<string> _data = new RefObjectPublisher<string>() { Object = "Initial" };
        public SampleModel2()
        {
            _aop = new ActiveObjectPart("SampleModel2") { ServiceFunc = Poll };
            ModelObserverCollection.AddModelObserverToCollection(new Dictionary<string, IRefObjectObserver>() { { $"{_aop.Name}.Data", new RefObjectObserver<string>(_data) } });
            _aop.Initialize();
        }

        char[] _status = new char[] { '\\', '|', '/', '-', };
        byte _statusIndex = 0;
        private void Poll()
        {
            _statusIndex++;
            if (_statusIndex >= _status.Length) { _statusIndex = 0; }
            _data.Object = $"TestApp#Time:{DateTime.Now}#RunningStatus:{_status[_statusIndex]}";
        }
    }
    #endregion

    public class TestCommand : System.Windows.Input.ICommand
    {
        public CommandDispatchManager Cdm { get; private set; }
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            var x = parameter as Dictionary<string, object>;
            var p = x[CtrlExtn.Parameters] as Dictionary<string, object>;
            Cdm.DispatchCommand(x[CtrlExtn.ModuleName] as string, x[CtrlExtn.CommandName] as string, new Dictionary<string, string>()).Start();
        }

        public TestCommand(CommandDispatchManager cdm)
        {
            Cdm = cdm;
        }
    }
}
