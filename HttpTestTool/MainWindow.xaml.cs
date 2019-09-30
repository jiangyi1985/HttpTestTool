using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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

namespace HttpTestTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //// Define the cancellation token.
        //static readonly CancellationTokenSource Source = new CancellationTokenSource();
        //private CancellationToken _token = Source.Token;

        public MainWindow()
        {
            InitializeComponent();

            //var tokenSource = new CancellationTokenSource();
            //token = tokenSource.Token;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var count = Int32.Parse(txtCount.Text);
            var url = txtUrl.Text.Trim();
            var mode = (selectMode.SelectedValue as ComboBoxItem).Name;
            var showThreadLog = cbShowThreadLog.IsChecked.Value;
            var showResponse = cbShowResponse.IsChecked.Value;
            var timeRange = Int32.Parse(txtTimeRange.Text);

            btnStart.IsEnabled = false;

            Task.Factory.StartNew(() => StartJob(count, url, mode, timeRange,showThreadLog,showResponse));
            //    .ContinueWith((task) =>
            //{
            //    Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        btnStart.IsEnabled = true;
            //    });
            //});
        }

        private void StartJob(int count, string url, string mode, int timeRange,bool showThreadLog, bool showResponse)
        {
            //Thread.Sleep(3000);
            //return;

            var setMinThreads = ThreadPool.SetMinThreads(count, count);
            if (!setMinThreads)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtOutput.AppendText("Set Min Threads Failed!\r\n");
                });
                return;
            }

            var r = new Random();

            //var url = txtUrl.Text.Trim();

            List<Task<double>> tasks = new List<Task<double>>();

            for (int i = 0; i < count; i++)
            {
                tasks.Add(new Task<double>(() =>
                {
                    var threadId = Thread.CurrentThread.ManagedThreadId;

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var request = WebRequest.CreateHttp(url);

                    //Thread.Sleep(r.Next(1000,2000));

                    HttpWebResponse response;
                    try
                    {
                        response = request.GetResponse() as HttpWebResponse;
                    }
                    catch (WebException ex)
                    {
                        response = ex.Response as HttpWebResponse;
                    }

                    var length = response.ContentLength;
                    var statusCode = response.StatusCode;
                    var responseStream = response.GetResponseStream();
                    var sr = new StreamReader(responseStream);
                    var str = sr.ReadToEnd();

                    stopwatch.Stop();

                    var elapsedTotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                    if(showThreadLog)
                    Application.Current.Dispatcher.Invoke(() =>
                        txtOutput.AppendText("thread: " + threadId + " " + "t: " + elapsedTotalMilliseconds +
                                             "ms " + statusCode + " " + length + "B "+(showResponse?str:"")+"\r\n"));

                    return elapsedTotalMilliseconds;
                }));
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText("Starting " + count + " client(s)...\r\n");
            });

            switch (mode)
            {
                case "burst":
                    tasks.ForEach(o => o.Start());
                    break;
                case "evenlyDistributed":
                    //var timeRange = Int32.Parse(txtTimeRange.Text);
                    var waitTimePerRequest = count == 1 ? 0 : (double)timeRange / (count - 1);
                    for (int i = 0; i < count; i++)
                    {
                        tasks[i].Start();

                        if (i != count - 1)
                            Thread.Sleep(TimeSpan.FromSeconds(waitTimePerRequest));
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Task.WaitAll(tasks.ToArray());

            
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var min = tasks.Min(o=>o.Result);
                    var max = tasks.Max(o => o.Result);
                    var avg = tasks.Average(o => o.Result);
                    txtOutput.AppendText("--------------------------------------------------------\r\n");
                    txtOutput.AppendText("avg: " + avg + "ms min: " + min + "ms max: " + max + "ms\r\n");
                    txtOutput.AppendText("--------------------------------------------------------\r\n");
                    txtOutput.AppendText("\r\n");
                });

                Application.Current.Dispatcher.Invoke(() =>
                    {
                        btnStart.IsEnabled = true;
                    });
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            //Source.Cancel();
        }
    }
}
