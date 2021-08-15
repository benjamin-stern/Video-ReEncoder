using ReEncode.Encode.Tasks;
using ReEncode.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode.Encode
{
    public class EncodeManager
    {
        private int _parallelJobs = 1;

        private List<string> _fileList;
        private int _fileIndex = 0;

        public delegate void ProcessFile(string path);
        public event ProcessFile OnProcessFile;
        public TargetEncodeFormat EncodeFormat { get; set; }
        public string InputPath { get; set; }
        public string OutputPath { get; set; }

        public float VMAFTarget { get; set; }
        public float VMAFOvershoot { get; set; }

        List<EncodeTaskAbstract> tasks = new List<EncodeTaskAbstract>();

        public delegate void AllTasksCompletedEventHandler();
        public event AllTasksCompletedEventHandler AllTasksCompleted;

        public delegate void TaskProcessingAmountChangedEventHandler(int amount);
        public event TaskProcessingAmountChangedEventHandler CurrentProcessedChanged;
        public event TaskProcessingAmountChangedEventHandler TotalToProcessChanged;

        public void StartProcessing()
        {
            var task = ConfigData.Instance.GetTask(InputPath);
            task.OutputPath = OutputPath;
            task.Data.VMAFTarget = VMAFTarget;
            task.Data.VMAFOvershootPercent = VMAFOvershoot;
            task.Data.EncodeType = EncodeFormat;
            ConfigData.Instance.Save();

            CountTotalTaskEncodings();

            switch (EncodeFormat)
            {
                case TargetEncodeFormat.H264:
                    break;
                case TargetEncodeFormat.H265xCPU:
                    _parallelJobs = 4;
                    break;
                case TargetEncodeFormat.H265xNVidia:
                    _parallelJobs = 4;
                    break;
                case TargetEncodeFormat.VP9:
                    break;
                case TargetEncodeFormat.AV1xLibAOM:
                    _parallelJobs = 4;
                    break;
                default:
                    _parallelJobs = 1;
                    break;
            }

            _fileList = FileOperation.GetRelativeNestedFileList(InputPath, new List<string> { ".mp4", ".mov", ".avi" });
            
            for (int i = 0; i < _parallelJobs; i++)
            {
                ProcessNext();
            }

            TotalToProcessChanged?.Invoke(_fileList.Count);
        }

        private void CountTotalTaskEncodings() {
            var task = ConfigData.Instance.GetTask(InputPath);
            int iterationCount = 0;
            int processedCount = 0;
            double compression = 0f;

            task.Data.ProcessTasks((in TaskDataFile f) => {
                processedCount ++;
                iterationCount += f.Encodes.Count;
                compression += f.Compression;
            });

            Log.i.Debug($"Task: Current Total Processed Files - {processedCount}, Encoding Iterations - {iterationCount}, Average Compression: {compression/processedCount}");
        }

        private void ProcessNext() {
            if (_fileList.Count > _fileIndex)
            {
                var file = _fileList[_fileIndex++];
                var task = ConfigData.Instance.GetTask(InputPath);
                task.OutputPath = OutputPath;
                task.Data.EncodeType = EncodeFormat;
                task.Data.VMAFTarget = VMAFTarget;
                task.Data.VMAFOvershootPercent =VMAFOvershoot;
                EncodeTaskAbstract encodeTask = null;
                switch (EncodeFormat)
                {
                    case TargetEncodeFormat.AV1xLibAOM:
                        encodeTask = new AV1xLibAOMEncodeTask(file, task);
                        break;
                    case TargetEncodeFormat.H265xCPU:
                        encodeTask = new H265xCpuEncodeTask(file, task);
                        break;
                    case TargetEncodeFormat.H264:
                    
                    case TargetEncodeFormat.VP9:
                    case TargetEncodeFormat.H265xNVidia:
                    default:
                        encodeTask = new H265xNVidiaEncodeTask(file, task);
                        break;
                }
                
                tasks.Add(encodeTask);
                encodeTask.OnComplete += () => { tasks.Remove(encodeTask); ProcessNext(); };
                encodeTask.OnError += () => { tasks.Remove(encodeTask); ProcessNext(); };
                encodeTask.Start();

                CurrentProcessedChanged?.Invoke(_fileIndex);
            }

            if (tasks.Count == 0) {
                AllTasksCompleted?.Invoke();
            }
        }

        //static private void RemoveAll<T>(MulticastDelegate eventHandler) where T:Delegate{
        //    foreach (Delegate d in eventHandler.GetInvocationList())
        //    {
        //        eventHandler -= (T.GetType())d;
        //    }
        //}

        public void StopProcessing() 
        {
            //RemoveAll<AllTasksCompletedEventHandler>(AllTasksCompleted);
            foreach (Delegate d in AllTasksCompleted.GetInvocationList())
            {
                AllTasksCompleted -= (AllTasksCompletedEventHandler)d;
            }

            foreach (Delegate d in CurrentProcessedChanged.GetInvocationList())
            {
                CurrentProcessedChanged -= (TaskProcessingAmountChangedEventHandler)d;
            }

            foreach (Delegate d in TotalToProcessChanged.GetInvocationList())
            {
                TotalToProcessChanged -= (TaskProcessingAmountChangedEventHandler)d;
            }

            CountTotalTaskEncodings();

            tasks.ForEach(t =>
            {
                t.Stop();
            });
            tasks.Clear();
        }
    }
}
