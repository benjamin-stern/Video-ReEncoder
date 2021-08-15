using ReEncode.Utils;

namespace ReEncode.Encode.Tasks
{
    class H265xCpuEncodeTask : EncodeTaskAbstract
    {
        public H265xCpuEncodeTask(string filePath, ConfigTask data) : base(filePath, data)
        {
        }

        public override TargetEncodeFormat EncodeFormat => TargetEncodeFormat.H265xCPU;

        public override int RATE_MAX => 63;

        public override int RATE_MIN => 1;

        public override int RATE_STEP => 1;

        public override string Extension => "mp4";

        override public ProcessSimpleRequest EncodeLossless {
            get {
                return new ProcessSimpleRequest()
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{InputPath}\" -c:v libx265 -preset veryfast -tune grain -pix_fmt yuv420p10le -x265-params lossless=1 {WorkingLosslessFile}"
                };
            }
        }

        override public ProcessSimpleRequest EncodeQuality {
            get {
                return new ProcessSimpleRequest()
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{InputPath}\" -c:v libx265 -preset veryfast -tune grain -pix_fmt yuv420p10le -crf {EncodeRateCurrent} {WorkingQualityFile}"
                };
            }
        }

        override public ProcessSimpleRequest EncodeFinal {
            get {
                return new ProcessSimpleRequest()
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{InputPath}\" -c:v libx265 -preset slower -tune grain -pix_fmt yuv420p10le -crf {EncodeRateCurrent} {WorkingQualityFile}"
                };
            }
        }
    }
}
