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

            Task.Factory.StartNew(() => StartJob(count, url, mode, timeRange, showThreadLog, showResponse));
            //    .ContinueWith((task) =>
            //{
            //    Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        btnStart.IsEnabled = true;
            //    });
            //});
        }

        private void StartJob(int count, string url, string mode, int timeRange, bool showThreadLog, bool showResponse)
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

            List<Task<Result>> tasks = new List<Task<Result>>();

            for (int i = 0; i < count; i++)
            {
                tasks.Add(new Task<Result>(() =>
                {
                    var threadId = Thread.CurrentThread.ManagedThreadId;

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var request = WebRequest.CreateHttp(url);
                    request.Timeout = 20000;//todo:set by UI

                    //Thread.Sleep(r.Next(1000,2000));

                    long length = 0;
                    HttpStatusCode statusCode = (HttpStatusCode)0;
                    string str = null;

                    HttpWebResponse response = null;
                    try
                    {
                        response = request.GetResponse() as HttpWebResponse;
                    }
                    catch (WebException ex)
                    {
                        if (ex.Response != null)
                            response = ex.Response as HttpWebResponse;
                        else
                            str = ex.Message;
                    }

                    if (response != null)
                    {
                        length = response.ContentLength;
                        statusCode = response.StatusCode;
                        var responseStream = response.GetResponseStream();
                        var sr = new StreamReader(responseStream);
                        str = sr.ReadToEnd();
                    }

                    stopwatch.Stop();

                    var elapsedTotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

                    if (showThreadLog)
                    {
                        if (response != null)
                            Application.Current.Dispatcher.Invoke(() =>
                                txtOutput.AppendText("thread: " + threadId + " " + "t: " + elapsedTotalMilliseconds +
                                                     "ms " + statusCode + " " + length + "B " + (showResponse ? str : "") +
                                                     "\r\n"));
                        else
                            Application.Current.Dispatcher.Invoke(() =>
                                txtOutput.AppendText("thread: " + threadId + " " + "t: " + elapsedTotalMilliseconds +
                                                     "ms " + str + "\r\n"));
                    }

                    return new Result()
                    {
                        ElapsedMilliseconds = elapsedTotalMilliseconds,
                        StatusCode = statusCode,
                    };
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
                var min = tasks.Min(o => o.Result.ElapsedMilliseconds);
                var max = tasks.Max(o => o.Result.ElapsedMilliseconds);
                var avg = tasks.Average(o => o.Result.ElapsedMilliseconds);
                var statusCodes = tasks.GroupBy(o => o.Result.StatusCode).OrderBy(o => o.Key)
                    .Select(o => (o.Key == 0 ? "Ex" : o.Key.ToString()) + ":" + o.Count()).Aggregate((o, n) => o + " " + n);
                txtOutput.AppendText("--------------------------------------------------------\r\n");
                txtOutput.AppendText("avg: " + avg + "ms min: " + min + "ms max: " + max + "ms\r\n"
                                     + statusCodes + "\r\n");
                txtOutput.AppendText("--------------------------------------------------------\r\n");
                txtOutput.AppendText("\r\n");
            });

            Application.Current.Dispatcher.Invoke(() =>
                {
                    btnStart.IsEnabled = true;
                });
        }

        class Result
        {
            public double ElapsedMilliseconds { get; set; }
            public HttpStatusCode StatusCode { get; set; }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            //Source.Cancel();
        }
    }
}
