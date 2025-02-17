using System.Diagnostics;

namespace MusicModder.Services
{
    public class XWBCreator
    {
        public string PacName { get; }

        public XWBCreator(string pacName)
        {
            PacName = pacName;
        }

        private string CompressAudioToAdpcm(string audioFilePath)
        {
            string ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");
            string tempWavFile = Path.Combine(Path.GetTempPath(), $"{PacName}.wav");
            string arguments = $"-i \"{audioFilePath}\" -c:a adpcm_ms -block_size 512 -ar 48000 -ac 2 -strict experimental -f wav \"{tempWavFile}\"";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new Exception("FFmpeg process could not be started.");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new Exception($"FFmpeg failed to process audio. Error: {error}");
                }
            }

            // Return the path to the temporary WAV file
            return tempWavFile;
        }

        private static MemoryStream ProcessXwbTool(string tempWavFile)
        {
            string xwbToolPath = Path.Combine(Directory.GetCurrentDirectory(), "XWBTool.exe");
            string tempXwbFile = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(tempWavFile)}.xwb");
            string arguments = $"-o \"{tempXwbFile}\" \"{tempWavFile}\" -f -nc";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = xwbToolPath,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process? process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new Exception("XWBTool process could not be started.");
                    }


                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception($"XWBTool failed to process audio. Error: {error}");
                    }
                }

                // Read the created XWB file into a memory stream
                MemoryStream xwbStream = new MemoryStream();

                using (var fileStream = new FileStream(tempXwbFile, FileMode.Open, FileAccess.Read))
                {
                    fileStream.CopyTo(xwbStream);
                }

                xwbStream.Seek(0, SeekOrigin.Begin); // Reset stream position
                return xwbStream;
            }
            finally
            {
                // Clean up temporary XWB file
                if (File.Exists(tempXwbFile)) File.Delete(tempXwbFile);
            }
        }

        public MemoryStream CreateXWBData(string audioFilePath)
        {
            if (string.IsNullOrEmpty(audioFilePath))
            {
                throw new ArgumentNullException(nameof(audioFilePath), "Audio file path cannot be null or empty.");
            }

            Console.WriteLine($"Compressing {Path.GetFileName(audioFilePath)}...");
            string tempWavFile = CompressAudioToAdpcm(audioFilePath);
            Console.WriteLine($"Finished compressing {Path.GetFileName(audioFilePath)}.");

            if (!File.Exists(tempWavFile))
            {
                throw new Exception($"Failed to compress {Path.GetFileName(audioFilePath)} into ADPCM format.");
            }

            try
            {
                Console.WriteLine($"Creating XWB from {Path.GetFileName(audioFilePath)}...");
                MemoryStream xwbData = ProcessXwbTool(tempWavFile);

                if (xwbData == null || xwbData.Length == 0)
                {
                    throw new Exception($"Failed to process {Path.GetFileName(audioFilePath)} into XWB format.");
                }

                Console.WriteLine($"Finished creating XWB from {Path.GetFileName(audioFilePath)}");

                return xwbData;
            }
            finally
            {
                // Clean up temporary files
                if (File.Exists(tempWavFile)) File.Delete(tempWavFile);
            }
        }
    }

}

