using ReEncode.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode.Encode.Tasks
{
    class H265xNVidiaEncodeTask : EncodeTaskAbstract
    {
        public H265xNVidiaEncodeTask(string filePath, ConfigTask data) : base(filePath, data)
        {
        }

        public override TargetEncodeFormat EncodeFormat => TargetEncodeFormat.H265xNVidia;

        public override int RATE_MAX => 51;

        public override int RATE_MIN => 1;

        public override int RATE_STEP => 1;

        public override string Extension => "mp4";

        override public ProcessSimpleRequest EncodeLossless {
            get {
                return new ProcessSimpleRequest() { 
                    applicationPath="../Tools/ffmpeg/ffmpeg.exe", 
                    arguments=$"-y -i \"{InputPath}\" -c:v hevc_nvenc -preset lossless -rc vbr_hq -gpu 0 -profile:v main10 -c:a copy {WorkingLosslessFile}" };
            }
        }

        override public ProcessSimpleRequest EncodeQuality {
            get {
                return new ProcessSimpleRequest()
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{InputPath}\" -c:v hevc_nvenc -rc vbr_hq -2pass 1 -preset slow -cq {EncodeRateCurrent} -profile:v main10 -tier high -level 4.1 -b:v 0 -c:a copy {WorkingQualityFile}"
                };
            }
        }

        static private H265xNVidiaEncodeTask lastTarget;
        protected override void Process()
        {
            if (lastTarget == null || lastTarget == this || lastTarget._state == ProcessTasks.TEST_VMAF || lastTarget._state == ProcessTasks.COMPLETE)
            {
                lastTarget = this;
                base.Process();
            }
            else
            {
                ExecutionPlan.Delay(3500, () => { Process(); });
            }

        }


    }
}
