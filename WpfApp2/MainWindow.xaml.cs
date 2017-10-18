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
        public MainWindow()
        {
            InitializeComponent();
            //Create first a basic model for startup(optional step)
            _sampleModel2 = new SampleModel2();

            //Create ViewModel Observer
            _modelObserverForVM = new ModelObserverForVM();
            //Create ViewHelper that binds to VM and View. Default the view to poll every 333ms
            _vh = new ViewHelper(this) { ViewModel = _modelObserverForVM, OnUiUpdate = UI_Update }.Initialize(ModelCollection.Models);

            //Now create the remaining models
            //OnButton_CreateModels(null, null);
        }

        private void UI_Update()
        {
        }

        private void OnButton_CreateModels(object sender, RoutedEventArgs e)
        {
            //Create Models
            _sampleModel1 = new SampleModel1();

            _vh.Initialize(ModelCollection.Models);
        }
    }

    #region Models
    public class SampleModel1
    {
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
            ModelCollection.AddModelObserverToCollection(new Dictionary<string, IRefObjectObserver>() { { $"{_aop.Name}.Data", new RefObjectObserver<CoordinateData>(Data) } });
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
            ModelCollection.AddModelObserverToCollection(new Dictionary<string, IRefObjectObserver>() { { $"{_aop.Name}.Data", new RefObjectObserver<string>(_data) } });
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
}
