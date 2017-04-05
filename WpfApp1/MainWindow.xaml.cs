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

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ActiveObjectPart _aop;
        DispatcherTimer _dt;
        public MainWindow()
        {
            InitializeComponent();
            _aop = new ActiveObjectPart("test", TimeSpan.FromMilliseconds(100));
            string ec = _aop.Initialize();
            _dt = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(333) };
            _dt.Tick += UI_Update;
            _dt.Start();
        }

        private List<ICommandStatus> _commands = new List<ICommandStatus>(100);
        private void UI_Update(object sender, EventArgs e)
        {
            _data.Text = _multiCommandResult;
            _dataGrid.ItemsSource = _commands.ToArray();
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
            if (icp == null) { icp = _aop.CreateCommand("MULTICOMMAND", () => RunCommand(_index)); _commands.Add(icp.Start()); }
            else { icp.Start(); }
            //MessageBox.Show(_aop.CreateCommand("Multi", () => RunCommand(_index)).Run());
            System.Diagnostics.Trace.WriteLine("Test_Click{0}".FormatEx(i));
        }

        public string _multiCommandResult { get; set; } = "Empty";
        private string RunCommand(int i)
        {
            for (long j = 0; j < 2000; ++j)
                for (long k = 0; k < 500000; ++k) { }
            _multiCommandResult =  "MultiCommand: Index ={0}".FormatEx(i);
            System.Diagnostics.Trace.WriteLine(_multiCommandResult);
            return _multiCommandResult;
        }

        private int _indexMulti = 0;
        private void Test_ClickMulti(object sender, RoutedEventArgs e)
        {
            int i = ++_indexMulti;
            _dataInit.Text = _indexMulti.ToString();
            _commands.Add(_aop.CreateCommand("MULTICOMMAND", () => RunCommand(i)).Start());
            System.Diagnostics.Trace.WriteLine("Test_Click{0}".FormatEx(i));
        }
    }
}
