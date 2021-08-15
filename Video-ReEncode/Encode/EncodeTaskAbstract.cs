using ReEncode.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode.Encode
{
    public enum ProcessTasks { 
        RETRIEVE_INFO,
        LOSSLESS_ENCODE,
        QUALITY_ENCODE,
        TEST_VMAF,
        FINAL_ENCODE,
        STAMP_INFO,
        COMPLETE
    }

    abstract class EncodeTaskAbstract
    {
        public EncodeTaskAbstract(string filePath, ConfigTask data) {
            _filePath = filePath;
            _data = data;

            EncodeRateMin = RATE_MIN;
            EncodeRateMax = RATE_MAX;
        }

        public delegate void Complete();
        public event Complete OnComplete;
        public event Complete OnError;
        public abstract TargetEncodeFormat EncodeFormat { get; }

        public abstract int RATE_MAX { get; }

        public abstract int RATE_MIN { get; }
        public abstract int RATE_STEP { get; }

        public int EncodeRateMin { get; set; }
        public int EncodeRateMax { get; set; }
        public int EncodeRateCurrent { get; set; } = -1;


        protected bool _isStarted;
        protected ConfigTask _data;
        protected string _filePath;
        protected TaskDataFile _fileData;
        protected ProcessTasks _state = ProcessTasks.RETRIEVE_INFO;
        protected Process _currentTask;
        protected ProcessCompleteResult _lastEncodeResult;
        protected string _vmafResult = "";

        public string InputPath { get { return Path.Combine(_data.InputPath, _filePath); } }
        public string OutputPath { get { return Path.Combine(_data.OutputPath, Path.ChangeExtension(_filePath, Extension)); } }
        public string OutputPathTemp { get { return Path.Combine(_data.OutputPath, Path.ChangeExtension(_filePath, Extension))+".tmp"; } }
        public string OutputWorkingPath { get {return Path.Combine(_data.OutputPath, Path.ChangeExtension(_filePath, WorkingExtension));} }
        public string WorkingPath { get { return Path.Combine(AppContext.BaseDirectory, "Work"+Path.DirectorySeparatorChar); } }
        public string WorkingLosslessFile { get { return Path.Combine(WorkingPath, $"{_fileData.Id}.lossless.{Extension}"); } }
        public string WorkingQualityFile { get { return Path.Combine(WorkingPath, $"{_fileData.Id}.test.{Extension}"); } }
        public abstract string Extension { get; }
        virtual public string WorkingExtension { get { return $"{EncodeFormat.ToString().ToLower()}.{Extension}"; } }

        virtual public ProcessSimpleRequest EncodeRetrieveInfo {
            get {
                return new ProcessSimpleRequest { applicationPath = "../Tools/ffmpeg/ffprobe.exe", arguments= $"-hide_banner -show_streams -select_streams v \"{InputPath}\"" };
            }
        }
        virtual public ProcessSimpleRequest EncodeLossless
        {
            get
            {
                return null;
            }
        }

        virtual public ProcessSimpleRequest EncodeQuality
        {
            get
            {
                return null;
            }
        }

        virtual public ProcessSimpleRequest EncodeTestVMAF
        {
            get
            {
                var vmafPath = $"{AppContext.BaseDirectory.Replace("\\", "/")}../Tools/vmaf/vmaf_v0.6.1.json";
                vmafPath = vmafPath.Replace(":", "\\\\:");

                return new ProcessSimpleRequest
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{WorkingLosslessFile}\" -i \"{WorkingQualityFile}\" -lavfi libvmaf=model_path=\"{vmafPath}\" -f null -"
                };
            }
        }

        virtual public ProcessSimpleRequest EncodeFinal {
            get { return null; }
        }

        virtual public ProcessSimpleRequest CopyMetadata {
            get {
                return new ProcessSimpleRequest
                {
                    applicationPath = "../Tools/exiftool.exe",
                    arguments = $"-TagsFromFile \"{InputPath}\" -XMP-dc:Creator=\"ReEncode\" -XMP-dc:Type=\"VMAF-{_vmafResult}\" \"{WorkingQualityFile}\""
                    //arguments = $"-XMP-dc:Creator=\"ReEncode\" -XMP-dc:Type=\"VMAF-{_vmafResult}\" \"{WorkingQualityFile}\""
                };
            }
        }

        /// <summary>
        /// Determines if the supplied vmaf is within the target range, 0 = yes, -1 = less, 1 = greater
        /// </summary>
        /// <param name="testVMAF"></param>
        /// <returns>0 = yes, -1 = less, 1 = greater</returns>
        virtual public int GetVMAFRange(float testVMAF) {
            int result = -1;
            float minVmaf = _data.Data.VMAFTarget;
            float maxVmaf = _data.Data.VMAFTarget + _data.Data.VMAFOvershootPercent;

            if ((Math.Min(maxVmaf, Math.Max(testVMAF, minVmaf)) == testVMAF))
            {
                result = 0;
            }
            else if (testVMAF > maxVmaf) {
                result = 1;
            }

            return result;
        }

        public void Start() {
            if (!_isStarted)
            {
                _isStarted = true;
                if (File.Exists(OutputPath) || (File.Exists(OutputPathTemp) && !_data.Data.HasTask(_filePath)))
                {
                    OnComplete?.Invoke();
                    return;
                }

                FileOperation.SaveFile(OutputPathTemp,"");
                _fileData = _data.Data.AddTask(_filePath);
                //var completedEncodeDetails = _fileData.Encodes.Find((e) =>
                //    (Math.Min(_data.Data.VMAFTarget + _data.Data.VMAFOvershootPercent, Math.Max(e.VMAFScore, _data.Data.VMAFTarget)) == e.VMAFScore)    
                //);

                bool isCompleted = false;
                float LastVMAF = -1f;
                _fileData.Encodes.ForEach((e) => {
                    int VMAFDirection = GetVMAFRange(e.VMAFScore);

                    if (VMAFDirection == 0)
                    {
                        EncodeRateCurrent = e.EncodeRate;
                        isCompleted = true;
                    }
                    else if (VMAFDirection == 1 && e.EncodeRate < EncodeRateMax)
                    {
                        EncodeRateMax = e.EncodeRate;
                    }
                    else if (VMAFDirection == -1 && e.EncodeRate > EncodeRateMin) {
                        EncodeRateMin = e.EncodeRate;
                    }

                    if (!isCompleted) {
                        EncodeRateCurrent = e.EncodeRate;
                        LastVMAF = e.VMAFScore;
                    }

                });

                if (isCompleted) {
                    _state = ProcessTasks.FINAL_ENCODE;
                } else if (EncodeRateCurrent != -1) {
                    EncodeRateCurrent = _data.Data.PredictNextRate(_fileData.Width, _fileData.Height, EncodeRateCurrent, EncodeRateMax, EncodeRateMin, RATE_STEP, LastVMAF, _data.Data.VMAFOvershootPercent);
                }

                Process();
            }
        }

        public void Stop() {
            try
            {
                _currentTask?.Kill();
                _currentTask?.Close();
                _currentTask?.Dispose();
            }
            catch (Exception ex){
                Debug.WriteLine($"Stop: {InputPath} exception {ex.ToString()}");
            }

            ExecutionPlan.Delay(500, () => {
                try
                {
                    File.Delete(WorkingQualityFile);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Stop: {InputPath}: WorkingQualityFile Delete exception {ex.ToString()}");
                }

                try
                {
                    File.Delete(WorkingLosslessFile);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Stop: {InputPath}: WorkingLosslessFile Delete exception {ex.ToString()}");
                }

                try
                {
                    File.Delete(OutputPathTemp);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Stop: {InputPath}: OutputPathTemp Delete exception {ex.ToString()}");
                }
            });
        }

        virtual protected void Process() {
            Log.i.Info($"Process: Id - {_fileData.Id}, STATE: {_state.ToString()}");

            switch (_state)
            {
                case ProcessTasks.RETRIEVE_INFO:
                    _currentTask = ProcessHelper.RunCommand(EncodeRetrieveInfo, (result)=> {
                        var textpair = new TextPairParser(result.Response, default);
                        int Width = -1;
                        int Height = -1;
                        textpair.GetValue("width", out Width);
                        textpair.GetValue("height", out Height);

                        _fileData.Width = Width;
                        _fileData.Height = Height;
                        _data.SaveData();

                        _state = ProcessTasks.LOSSLESS_ENCODE;
                        Process();
                    });
                    
                    break;
                case ProcessTasks.LOSSLESS_ENCODE:
                    if (EncodeLossless != null)
                    {
                        FileInfo fileInfo = new FileInfo(WorkingPath);
                        if (!fileInfo.Directory.Exists) fileInfo.Directory.Create();

                        _currentTask = ProcessHelper.RunCommand(EncodeLossless, (result) => {
                            _state = ProcessTasks.QUALITY_ENCODE;
                            Process();
                        });
                    }
                    else {
                        OnError.Invoke();
                    }
                    break;
                case ProcessTasks.QUALITY_ENCODE:
                    if (EncodeQuality != null)
                    {
                        if (EncodeRateCurrent == -1)
                        {
                            Log.i.Info($"---- Start PredictNextRate: ID: {_fileData.Id}, path: {_fileData.Path}");
                            EncodeRateCurrent = _data.Data.PredictNextRate(_fileData.Width, _fileData.Height, EncodeRateCurrent, EncodeRateMax, EncodeRateMin, RATE_STEP, _data.Data.VMAFTarget, _data.Data.VMAFOvershootPercent);
                            Log.i.Info($"---- End PredictNextRate: {_fileData.Path}");
                        }
                        
                        Log.i.Info($"EncodeRate: {EncodeRateCurrent}");
                        _currentTask = ProcessHelper.RunCommand(EncodeQuality, (result) => {
                            _lastEncodeResult = result;
                            _state = ProcessTasks.TEST_VMAF;
                            Process();
                        });
                    }
                    else
                    {
                        OnError.Invoke();
                    }
                    break;
                case ProcessTasks.TEST_VMAF:
                    if (EncodeTestVMAF != null)
                    {
                        _currentTask = ProcessHelper.RunCommand(EncodeTestVMAF, (result) => {
                            var textpair = new TextPairParser(result.Error, default);
                            float VMAFScore = -1;
                            var keys = textpair.GetKeysContains("vmaf score");
                            if (keys.Count > 0)
                            {
                                textpair.GetValue(keys[0], out VMAFScore);

                                Debug.WriteLine($"Id - {_fileData.Id}, result.Response: {result.Response}");
                                Log.i.Info($"Id - {_fileData.Id}, VMAFScore: {VMAFScore.ToString()}");
                            }

                            _fileData.Encodes.Add(new TaskDataEncode() { EncodeRate = EncodeRateCurrent, VMAFScore = VMAFScore, EncodeSeconds = _lastEncodeResult.Duration });
                            _data.SaveData();

                            if (GetVMAFRange(VMAFScore) == 0) {
                                _state = ProcessTasks.FINAL_ENCODE;
                                Process();

                            } else {
                                if (VMAFScore < _data.Data.VMAFTarget)
                                {
                                    EncodeRateMax = EncodeRateCurrent - RATE_STEP;
                                }
                                else {
                                    EncodeRateMin = EncodeRateCurrent + RATE_STEP;
                                }
                                
                                Log.i.Debug($"VMAF Complete > Predicting Next Encode: Id - {_fileData.Id}, path: {_fileData.Path}");
                                var nextAttempt = _data.Data.PredictNextRate(_fileData.Width, _fileData.Height, EncodeRateCurrent, EncodeRateMax, EncodeRateMin, RATE_STEP, VMAFScore, 0.25f);
                                if (nextAttempt != EncodeRateCurrent)
                                {
                                    EncodeRateCurrent = nextAttempt;
                                    _state = ProcessTasks.QUALITY_ENCODE;
                                    Process();
                                }
                                else {
                                    TaskDataEncode closestValue = null;
                                    _fileData.Encodes.ForEach((e) => {
                                        if (e.VMAFScore > _data.Data.VMAFTarget && (closestValue == null || e.VMAFScore < closestValue.VMAFScore )) {
                                            closestValue = e;
                                        }
                                    });

                                    if (closestValue != null)
                                    {
                                        EncodeRateCurrent = closestValue.EncodeRate;
                                        _state = ProcessTasks.FINAL_ENCODE;
                                        Process();
                                    }
                                    else {
                                        OnError.Invoke();
                                    }
                                }
                            }
                        });
                    }
                    else
                    {
                        OnError.Invoke();
                    }
                    break;
                case ProcessTasks.FINAL_ENCODE:
                    if (EncodeFinal != null || EncodeQuality != null)
                    {
                        Log.i.Info($"EncodeRate: {EncodeRateCurrent}");
                        _currentTask = ProcessHelper.RunCommand(EncodeFinal ?? EncodeQuality, async (_) => {
                            await Task.Delay(1000);
                            Log.i.Info($"FINAL TESTING - VMAF - Id - {_fileData.Id}");
                            _currentTask = ProcessHelper.RunCommand(EncodeTestVMAF, (result) =>
                            {
                                var textpair = new TextPairParser(result.Error, default);
                                float VMAFScore = -1;
                                var keys = textpair.GetKeysContains("vmaf score");
                                if (keys.Count > 0)
                                {
                                    textpair.GetValue(keys[0], out VMAFScore);
                                    _vmafResult = VMAFScore.ToString();
                                    Debug.WriteLine($"FINAL VMAF - Id - {_fileData.Id}, result.Response: {result.Response}");
                                    Log.i.Info($"FINAL VMAF - Id - {_fileData.Id}, VMAFScore: {VMAFScore}");
                                }
                                else {
                                    Log.i.Warn($"FINAL VMAF - Id - {_fileData.Id}, Not generated");
                                }

                                _state = ProcessTasks.STAMP_INFO;
                                Process();
                            });
                        });
                    }
                    else
                    {
                        _state = ProcessTasks.COMPLETE;
                        Process();
                    }
                    break;
                case ProcessTasks.STAMP_INFO:
                    //Ignore Stamping Data for Testing Purposes.
                    _currentTask = ProcessHelper.RunCommand(CopyMetadata, (result) =>
                    {
                        _state = ProcessTasks.COMPLETE;
                        Process();
                    });
            break;
                case ProcessTasks.COMPLETE:
                    _fileData.IsComplete = true;
                    _fileData.Compression = ((double)new FileInfo(WorkingQualityFile).Length / (double)new FileInfo(InputPath).Length);
                    _data.SaveData();

                    File.Move(WorkingQualityFile, OutputPath);
                    File.Delete(WorkingLosslessFile);
                    File.Delete(OutputPathTemp);

                    OnComplete?.Invoke();
                    break;
            }
        }
    }
}
