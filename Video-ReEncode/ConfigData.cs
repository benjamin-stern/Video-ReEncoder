using Newtonsoft.Json;
using ReEncode.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode
{

    public enum TargetEncodeFormat { 
        H264,
        H265xCPU,
        H265xNVidia,
        VP9,
        AV1xLibAOM,
        AV1xRav1e
    }

    public class ConfigData
    {
        static private ConfigData _instance;
        private ConfigData() {

        }

        static public ConfigData Instance {
            get {
                if (_instance == null) {
                    _instance = Load();
                }
                return _instance;
            } 
        }

        public int LastIndex { get; set; } = 0;

        [JsonPropertyAttribute]
        private List<ConfigTask> Tasks { get; set; } = new List<ConfigTask>();

        public bool HasTask(string path) {
            return Tasks.Find((t) => t.InputPath == path) != null;
        }
        public ConfigTask GetLatestTask() {
            ConfigTask result = null;
            Tasks.ForEach((t) =>
            {
                if (result == null || t.LastUsed.Ticks > result.LastUsed.Ticks) {
                    result = t; 
                }
            });

            return result;
        }

        public ConfigTask GetTask(string path) {
            ConfigTask result = null;
            if (HasTask(path))
            {
                result = Tasks.Find((t) => t.InputPath == path);
            }
            else {
                result = new ConfigTask { InputPath = path, Index = ++LastIndex };
                Tasks.Add(result);
                Save();
            }

            result.LastUsed = DateTime.Now;

            return result;
        }

        public void Save() {
            FileOperation.SaveFile("tasks.config", JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        static private ConfigData Load() {
            var rawData = FileOperation.LoadFile("tasks.config");
            var result = JsonConvert.DeserializeObject<ConfigData>(rawData) ?? new ConfigData();
            return result;
        }
    }

    public class ConfigTask {
        public int Index { get; set; }
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public DateTime LastUsed { get; set; }

        [JsonIgnore]
        private TaskData _data;
        [JsonIgnore]
        public TaskData Data {
            get {
                if (_data != null) return _data;
                var rawData = FileOperation.LoadFile(GetTaskDataPath());
                return (_data = JsonConvert.DeserializeObject<TaskData>(rawData)??new TaskData());
            }
        }

        public string GetTaskDataPath() {
            return $"task-data.{Index}.config";
        }
        private readonly object saveDataLock = new object();
        public void SaveData() {
            lock (saveDataLock)
            {
                var rawData = JsonConvert.SerializeObject(_data, Formatting.Indented);
                FileOperation.SaveFile(GetTaskDataPath(), rawData);
            }
        }

        public void RemoveData() {
            if (FileOperation.MoveFile(GetTaskDataPath(), $"{GetTaskDataPath()}.{DateTime.Now.Ticks}")) {
                _data = null;
            }
        }
        
    }

    public class TaskData {
        public bool IsComplete { get; set; } = false;
        public bool IsError { get; set; } = false;

        [JsonPropertyAttribute]
        private List<TaskDataFile> List { get; set; } = new List<TaskDataFile>();
        public TargetEncodeFormat EncodeType { get; set; } = TargetEncodeFormat.AV1xLibAOM;
        public float VMAFTarget { get; set; } = 95;
        public float VMAFOvershootPercent { get; set; } = 1;

        [JsonPropertyAttribute]
        private int Count = 0;
        
        public TaskDataFile GetTask(string path) {
            return List.Find(tdf => tdf.Path == path);
        }

        public bool HasTask(string path) {
            return GetTask(path) != null;
        }

        public TaskDataFile AddTask(string filePath) {
            //TODO: Add Mutex Lock related to file saving

            var task = GetTask(filePath);
            if (task == null) {
                task = new TaskDataFile { Id = ++Count, Path = filePath };
                List.Add(task);
            }
            return task;
        }

        public delegate void ProcessTaskAction(in TaskDataFile task);
        public void ProcessTasks(ProcessTaskAction action) {
            for (int i = 0; i < List.Count; i++)
            {
                action(List[i]);
            }
        }
        
        public int PredictNextRate(int Width, int Height, int LastEncodeRate, int EncodeRateMax, int EncodeRateMin, int EncodeRateStep, float VMAFScore, float VMAFRange)
        {
            Log.i.Info($"PredictNextRate: Width - {Width}, Height - {Height}, LastEncodeRate - {LastEncodeRate}, EncodeRateMax - {EncodeRateMax}, EncodeRateMin - {EncodeRateMin}, VMAFScore - {VMAFScore}, VMAFRange - {VMAFRange} ");
            int newTargetRate = -1;
            int newTargetCount = -1;

            List<int> PredictiveRateList = new List<int>();

            if (LastEncodeRate != -1)
            {
                Log.i.Info($"Optimizing based on last encode ");

                List.ForEach((t) =>
                {
                    if (
                        t.IsComplete &&
                        Width == t.Width &&
                        Height == t.Height &&
                        t.Encodes.GetRange(0, t.Encodes.Count - 1).Find((e) =>
                        {
                            return e.EncodeRate == LastEncodeRate && 
                                ((e.VMAFScore - VMAFScore) is float diff) && 
                                diff <= VMAFRange && 
                                diff >= 0;
                        }) != null)
                    {
                        var encode = t.Encodes.Last();
                        Log.i.Info($"option: rate - {encode.EncodeRate}, score - {encode.VMAFScore}, time - {encode.EncodeSeconds}");

                        PredictiveRateList.Add(encode.EncodeRate);
                    }
                });
            }
            else {
                Log.i.Info($"First Encoding: Predict most likely candidate based off of previous encodings ");

                List.ForEach((t) =>
                {

                    if (
                        t.IsComplete &&
                        Width == t.Width &&
                        Height == t.Height &&
                        t.Encodes.Last() is TaskDataEncode encode &&
                        Math.Max(VMAFScore, Math.Min(encode.VMAFScore, VMAFScore+VMAFRange)) == encode.VMAFScore
                       )
                    {
                        PredictiveRateList.Add(encode.EncodeRate);
                    }
                });
            }
            
            PredictiveRateList.Distinct().ToList().ForEach((rate) =>
            {
                int count = PredictiveRateList.Count((r) => { return r == rate; });
                Log.i.Info($"Candidate: count: {count}, rate: {rate} ");
                if (count > newTargetCount && Math.Max(Math.Min(rate, EncodeRateMax), EncodeRateMin) == rate) {
                    newTargetCount = count;
                    newTargetRate = rate;
                }
            });

            return newTargetRate != -1 ? newTargetRate : (EncodeRateMin + ((EncodeRateMax - EncodeRateMin) / 2));
        }
    }
    public class TaskDataFile {
        public int Id { get; set; }
        public bool IsComplete { get; set; } = false;
        public string Path { get; set; }
        public List<TaskDataEncode> Encodes { get; set; } = new List<TaskDataEncode>();
        public int Width { get; set; }
        public int Height { get; set; }
        public double Compression { get; set; } = 1;
    }

    public class TaskDataEncode { 
        public int EncodeRate { get; set; }
        public float VMAFScore { get; set; }
        public float EncodeSeconds { get; set; } = 0;
    }
}
