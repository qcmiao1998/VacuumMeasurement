using CsvHelper;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace VacuumMeasurement
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel viewModel;
        private SerialPort? serialPort;
        public MainWindow()
        {
            InitializeComponent();
            viewModel = (MainViewModel)DataContext;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.ButtonValue == "Stop")
            {
                comSelect.IsEnabled = true;
                baudrate.IsEnabled = true;
                interval.IsEnabled = true;
                viewModel.ButtonValue = "Start";
                                
                serialPort?.Close();
                return;
            }
            if (!SelectSavePath())
            {
                MessageBox.Show("Select saving path first.","Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                serialPort = new SerialPort(comSelect.Text, int.Parse(baudrate.Text));
                serialPort.ReadBufferSize = 64;
                serialPort.Open();
            }
            catch (Exception)
            {
                MessageBox.Show($"Serial error, cannot open \"{comSelect.Text}\".", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return ;
            }
            comSelect.IsEnabled = false;
            baudrate.IsEnabled = false;
            interval.IsEnabled = false;
            viewModel.ButtonValue = "Stop";
            var path = savePath.Text;
            var intv = double.Parse(interval.Text);

            Task.Run(() =>
            {
                using var writer = new StreamWriter(path);
                writer.AutoFlush = true;
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                
                csv.WriteHeader<Data>();
                csv.NextRecord();

                var series = (LineSeries)viewModel.ValueModel.Series[0];
                
                while (viewModel.ButtonValue == "Stop")
                {
                    double value = GetData();
                    if (value == double.MaxValue) continue;

                    Data data = new Data(value);
                    csv.WriteRecord(data);
                    csv.NextRecord();
                    series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), value));
                    if (series.Points.Count > 10000)
                    {
                        series.Points.RemoveAt(0);
                    }
                    viewModel.ValueModel.InvalidatePlot(true);
                    Thread.Sleep(TimeSpan.FromSeconds(intv));
                }
            });
        }

        private void plotView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            plotView.ResetAllAxes();
        }

        private bool SelectSavePath()
        {
            SaveFileDialog dialog = new()
            {
                Title = "Saving Path",
                DefaultExt = "csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            
            if (dialog.ShowDialog() == true)
            {
                savePath.Text = dialog.FileName;
                return true;
            }
            return false;
        }

        private double GetData()
        {
            if (serialPort == null)
            {
                throw new NullReferenceException();
            }

            try
            {
                serialPort.ReadTimeout = 5000;
                serialPort.Write("RPV3\r");
                var outstr = serialPort.ReadTo("\r");
                
                if (outstr[0] != '0')
                {
                    return double.MaxValue;
                }
                return double.Parse(outstr.Substring(3), NumberStyles.Any);
            }
            catch (Exception)
            {
                
            }

            return double.MaxValue;
        }

        struct Data
        {
            public Data(double value)
            {
                Value = value;
            }
            public DateTime Time { get; set; } = DateTime.Now;
            public double Value { get; set; }
        }
    }

    public class MainViewModel: INotifyPropertyChanged
    {
        public string[] AvailablePorts => SerialPort.GetPortNames();
        public PlotModel ValueModel { get; set; }
        private string startBtnContent = "Start";
        public string ButtonValue
        {
            get => startBtnContent;
            set
            {
                startBtnContent = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            ValueModel = new PlotModel();
            ValueModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom});
            ValueModel.Series.Add(new LineSeries());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
