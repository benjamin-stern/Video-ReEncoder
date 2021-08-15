using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode.Utils
{
    class ProcessCompleteResult
    {
        public string Response { get; set; } = "";
        public string Error { get; set; } = "";
        public int ExitCode { get; set; } = 0;
        public DateTime ExitTime { get; set; }
        public DateTime StartTime { get; set; }
        public long Duration
        {
            get
            {
                return (ExitTime - StartTime).Ticks / TimeSpan.TicksPerSecond;
            }
        }

        public override string ToString()
        {
            return $"ProcessCompleteResult: ---" +
                $"Response: {Response} \r\n" +
                $"Error: {Error} \r\n" +
                $"ExitCode: {ExitCode} \r\n" +
                $"Duration: {Duration} \r\n";
        }
    }

    class ProcessSimpleRequest {
        public string applicationPath;
        public string arguments;
    }

    static class ProcessHelper
    {
        public delegate void CommandCallback(ProcessCompleteResult result);

        static public Process RunCommand(ProcessSimpleRequest request, CommandCallback callback) {
            return RunCommand(request.applicationPath, request.arguments, callback);
        }

        static public Process RunCommand(string applicationPath, string arguments, CommandCallback callback)
        {

            Debug.WriteLine($"RunCommand: {applicationPath} {arguments}");
            var task = new Process();

            task.StartInfo.UseShellExecute = false;
            task.StartInfo.CreateNoWindow = true;
            task.StartInfo.RedirectStandardOutput = true;
            task.StartInfo.RedirectStandardError = true;
            task.StartInfo.FileName = applicationPath;
            task.StartInfo.Arguments = arguments;
            task.StartInfo.WorkingDirectory = AppContext.BaseDirectory;
            
            var error = "";
            
            task.ErrorDataReceived += (sender, e) => {
                
                try
                {
                    error += e.Data + "\r\n";
                }
                catch (Exception ex)
                {
                    error = ex.ToString();
                }
            };
            var response = "";
            task.OutputDataReceived += (sender, e) =>
            {
                try
                {
                    response += e.Data+"\r\n";
                }
                catch (Exception ex)
                {
                    response = ex.ToString();
                }
            };
            
            task.Start();
            task.PriorityClass = ProcessPriorityClass.Idle;
            //p.WaitForExit();
            task.EnableRaisingEvents = true;

            task.BeginErrorReadLine();
            task.BeginOutputReadLine();

            EventHandler finishedEvent = (object sender, EventArgs e) =>
            {
                
                var result = new ProcessCompleteResult
                {
                    Response = response,
                    Error = error,
                    ExitCode = task.ExitCode,
                    StartTime = task.StartTime,
                    ExitTime = task.ExitTime
                };

                Debug.WriteLine($"{result.ToString()}");
                task.Close();
                task.Dispose();

                callback?.Invoke(result);
            };
            task.Exited += finishedEvent;
            //task.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
            //    finishedEvent(sender, null);
            //};
            

            return task;
        }
    }
}
