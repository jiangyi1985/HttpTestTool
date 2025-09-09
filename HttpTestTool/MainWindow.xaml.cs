using System;
using System.Collections.Concurrent;
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

        ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        ConcurrentBag<DateTime> _dtStartList, _dtEndList;
        private string _postData;

        public MainWindow()
        {
            InitializeComponent();

            //var setMinThreads = ThreadPool.SetMinThreads(1000, 1000);

            //var tokenSource = new CancellationTokenSource();
            //token = tokenSource.Token;

            Task.Run(() =>
            {
                while (true)
                {
                    if (_logQueue.Count > 0)
                    {
                        //Console.WriteLine($"outputing {_logQueue.Count} logs...");
                        List<string> list = new List<string>();
                        while (_logQueue.Count > 0)
                        {
                            _logQueue.TryDequeue(out string msg);
                            list.Add(msg);
                        }
                        var text = list.Aggregate((a, b) => a + Environment.NewLine + b);
                        Application.Current.Dispatcher.Invoke(() =>
                                txtOutput.AppendText(text + Environment.NewLine));
                    }
                    else
                    {
                        //Console.WriteLine("no log to output. sleeping...");
                        Thread.Sleep(500);
                    }

                    //Console.WriteLine(ThreadPool.GetAvailableThreads);
                }
            });
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var count = Int32.Parse(txtCount.Text);
            var url = txtUrl.Text.Trim();
            var mode = (selectMode.SelectedValue as ComboBoxItem).Name;
            var showThreadLog = cbShowThreadLog.IsChecked.Value;
            var showResponse = cbShowResponse.IsChecked.Value;
            var timeRange = Int32.Parse(txtTimeRange.Text);
            var method = ((ComboBoxItem)selectMethod.SelectedItem).Content.ToString();

            btnStart.IsEnabled = false;

            Task.Factory.StartNew(() => StartJob(url, method, count, mode, timeRange, showThreadLog, showResponse));
            //    .ContinueWith((task) =>
            //{
            //    Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        btnStart.IsEnabled = true;
            //    });
            //});
        }

        private void StartJob(string url, string method,int count, string mode, int timeRange, bool showThreadLog, bool showResponse)
        {
            //Thread.Sleep(3000);
            //return;

            _dtStartList = new ConcurrentBag<DateTime>();
            _dtEndList = new ConcurrentBag<DateTime>();

            //var setMinThreads = ThreadPool.SetMinThreads(count+10, count + 10);
            //if (!setMinThreads)
            //{
            //    Log("Set Min Threads Failed!");
            //    Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        btnStart.IsEnabled = true;
            //    });
            //    return;
            //}

            var r = new Random();

            //var url = txtUrl.Text.Trim();

            List<Task<Result>> tasks = new List<Task<Result>>();

            //for (int i = 0; i < count; i++)
            //{
            //    tasks.Add(funcHttpClient(url, showThreadLog, showResponse));
            //}

            Log("Starting " + count + " client(s)...");

            switch (mode)
            {
                case "burst":
                    //tasks.ForEach(o => o.Start());

                    for (int i = 0; i < count; i++)
                    {
                        tasks.Add(TaskHttpClient(url,method, showThreadLog, showResponse, i + 1));
                    }
                    break;
                case "evenlyDistributed":
                    //var timeRange = Int32.Parse(txtTimeRange.Text);
                    var waitTimePerRequest = count == 1 ? 0 : (double)timeRange / (count - 1);
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    for (int i = 0; i < count; i++)
                    {
                        //tasks[i].Start();

                        tasks.Add(TaskHttpClient(url, method, showThreadLog, showResponse, i + 1));

                        if (i != count - 1)
                        {
                            //Thread.Sleep(TimeSpan.FromSeconds(waitTimePerRequest));

                            var elapsed = stopwatch.ElapsedMilliseconds;
                            var expectedNext = (i + 1) * waitTimePerRequest * 1000;
                            var delay = expectedNext - elapsed;
                            if (delay < 0) delay = 0;
                            //Console.WriteLine($"elapsed {elapsed} expectedNext {expectedNext} delay {delay}");
                            Thread.Sleep((int)delay);
                        }
                    }
                    stopwatch.Stop();

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Task.WaitAll(tasks.ToArray());

            var min = tasks.Min(o => o.Result.ElapsedMilliseconds);
            var max = tasks.Max(o => o.Result.ElapsedMilliseconds);
            var avg = tasks.Average(o => o.Result.ElapsedMilliseconds);
            var statusCodeSummary = tasks.GroupBy(o => o.Result.StatusCode).OrderBy(o => o.Key)
                .Select(o => (o.Key == 0 ? "Ex" : o.Key.ToString()) + ":" + o.Count()).Aggregate((o, n) => o + " " + n);
            var minStart = _dtStartList.Min(t => t);
            var maxStart = _dtStartList.Max(t => t);
            var minEnd = _dtEndList.Min(t => t);
            var maxEnd = _dtEndList.Max(t => t);

            Log("--------------------------------------------------------");
            Log("Avg: " + avg + "ms Min: " + min + "ms Max: " + max + "ms");
            Log(statusCodeSummary);
            Log($"thread start:{minStart.ToString("HH:mm:ss.fff")}~{maxStart.ToString("HH:mm:ss.fff")} diff:{(maxStart - minStart).TotalSeconds}s");
            Log($"\tend:{minEnd.ToString("HH:mm:ss.fff")}~{maxEnd.ToString("HH:mm:ss.fff")} diff:{(maxEnd - minEnd).TotalSeconds}s");
            Log("--------------------------------------------------------");
            //ThreadPool.GetMinThreads(out int workThreads, out int completionPortThreads);
            //Log($"workThreads {workThreads} completionPortThreads {completionPortThreads}");
            Log("");

            Application.Current.Dispatcher.Invoke(() =>
                {
                    btnStart.IsEnabled = true;
                });
        }

        private async Task<Result> TaskHttpClient(string url,string method, bool showThreadLog, bool showResponse, int sequence)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;

            _dtStartList.Add(DateTime.Now);
            if (showThreadLog)
            {
                Log("thread: " + threadId + $" seq: {sequence} start...");
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var request = WebRequest.CreateHttp(url);
            request.Timeout = 20000;//todo:set by UI
            request.Method = method;
            request.AllowAutoRedirect = true;//todo:set by UI

            long length = 0;
            HttpStatusCode statusCode = (HttpStatusCode)0;
            string str = null;

            HttpWebResponse response = null;
            try
            {
                if (request.Method == "POST")
                {
                    request.ContentType = "application/json";
                    var reqStream = await request.GetRequestStreamAsync();
                    var sw = new StreamWriter(reqStream);
                    sw.Write(_postData);
                    sw.Close();
                    reqStream.Close();
                }
                //Thread.Sleep(r.Next(1000,2000));

                response = await request.GetResponseAsync() as HttpWebResponse;
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

            _dtEndList.Add(DateTime.Now);
            if (showThreadLog)
            {
                if (response != null)
                    Log("thread: " + threadId + $" seq: {sequence} t: " + elapsedTotalMilliseconds + "ms "
                        + statusCode + " " + length + "B " + (showResponse ? str : ""));
                else
                    Log("thread: " + threadId + $" seq: {sequence} t: " + elapsedTotalMilliseconds + "ms " + str);
            }

            return new Result()
            {
                ElapsedMilliseconds = elapsedTotalMilliseconds,
                StatusCode = statusCode,
            };
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

        private void selectMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtTimeRange != null)
            {
                if ((e.Source as ComboBox).SelectedItem.ToString().Contains("Distribute"))
                {
                    lblTimeRange.IsEnabled = true;
                    txtTimeRange.IsEnabled = true;
                }
                else
                {
                    lblTimeRange.IsEnabled = false;
                    txtTimeRange.IsEnabled = false;
                }
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (btnEditPost != null)
            {
                if ((e.Source as ComboBox).SelectedItem.ToString().Contains("POST"))
                {
                    btnEditPost.IsEnabled = true;
                }
                else
                {
                    btnEditPost.IsEnabled = false;
                }
            }
        }

        private void txtEditPost_Click(object sender, RoutedEventArgs e)
        {
            var w = new EditPostWindow();
            w.OnSave += text =>
            {
                _postData = text;
            };
            w.ShowDialog();
        }

        private void Log(string text)
        {
            _logQueue.Enqueue(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff") + " " + text);
        }
    }
}
