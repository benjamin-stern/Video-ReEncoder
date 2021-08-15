using ReEncode.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode.Encode.Tasks
{
    class AV1xRav1eEncodeTask : EncodeTaskAbstract
    {
        public AV1xRav1eEncodeTask(string filePath, ConfigTask data) : base(filePath, data)
        {
        }

        public override TargetEncodeFormat EncodeFormat => TargetEncodeFormat.AV1xRav1e;

        public override int RATE_MAX => 255;

        public override int RATE_MIN => 0;

        public override int RATE_STEP => 1;

        public override string Extension => "mp4";

        public enum BitDepth
        {
            _8Bit,
            _10Bit,
            _12Bit
        }

        public string BitDepthToPixelFormat(BitDepth depth)
        {
            string result = null;
            switch (depth)
            {
                case BitDepth._8Bit:
                    result = "yuv420p";
                    break;
                case BitDepth._10Bit:
                    result = "yuv420p10le";
                    break;
                case BitDepth._12Bit:
                    result = "yuv420p12le";
                    break;
                default:
                    break;
            }

            return result;
        }

        private BitDepth defaultDepth = BitDepth._10Bit;
        private string GetDefaultBitDepthCommand => $"-pix_fmt {BitDepthToPixelFormat(defaultDepth)}";


        override public ProcessSimpleRequest EncodeLossless {
            get {
                return new ProcessSimpleRequest()
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{InputPath}\" -c:v librav1e {GetDefaultBitDepthCommand} -speed 10 -b:v 0 -qp 0 {WorkingLosslessFile}"
                };
            }
        }

        override public ProcessSimpleRequest EncodeQuality {
            get {
                return new ProcessSimpleRequest()
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{InputPath}\" -c:v librav1e {GetDefaultBitDepthCommand} -qp {EncodeRateCurrent} -speed 10 -tiles 4x4 -b:v 0 {WorkingQualityFile}"
                };
            }
        }

        override public ProcessSimpleRequest EncodeFinal {
            get {
                return new ProcessSimpleRequest()
                {
                    applicationPath = "../Tools/ffmpeg/ffmpeg.exe",
                    arguments = $"-y -i \"{InputPath}\" -c:v librav1e {GetDefaultBitDepthCommand} -qp {EncodeRateCurrent} -speed 4 -b:v 0 {WorkingQualityFile}"
                };
            }
        }
    }
}
