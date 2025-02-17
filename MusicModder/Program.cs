using System.CommandLine;
using System.CommandLine.Help;
using System.Text;
using MusicModder.Services;

class Program
{
    static void printCommandHelp(RootCommand command)
    {
        using (var memoryStream = new MemoryStream())
        using (var sw = new StreamWriter(memoryStream))
        {
            var helpBuilder = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
            var helpContext = new HelpContext(helpBuilder, command, sw);
            helpBuilder.Write(helpContext);

            // Ensure all data is written to the memory stream
            sw.Flush();

            memoryStream.Seek(0, SeekOrigin.Begin);

            using (var sr = new StreamReader(memoryStream))
            {
                string helpText = sr.ReadToEnd();
                Console.WriteLine(helpText);
            }
        }

    }
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            var rootCommand = new RootCommand
            {
                new Option<int>("--track-volume", () => 200, "Set the volume for each track (0-255)."),
                new Option<int>("--sound-volume", () => 200, "Set the overall sound volume (0-255)."),
                new Argument<string[]>("files", "List of input files (.pac, .wav, .mp3, .mp4)")
            };

            printCommandHelp(rootCommand);

            Console.WriteLine("Drag and drop files onto this executable, or provide file paths as arguments.");
        }
        else
        {

            // Define volume options
            var trackVolumeOption = new Option<int>(
                "--track-volume",
                () => 200, // Default value
                "Set the volume for each track (0-255)."
            );

            var soundVolumeOption = new Option<int>(
                "--sound-volume",
                () => 200, // Default value
                "Set the overall sound volume (0-255)."
            );


            // Define the file options and arguments
            var fileArgument = new Argument<string[]>(
                name: "files",
                description: "List of input files (.pac, .wav, .mp3)"
            );
            // Create the root command
            var rootCommand = new RootCommand
        {
            trackVolumeOption,
            soundVolumeOption,
            fileArgument,
        };
            // Set the handler for the root command
            rootCommand.SetHandler((trackVolume, soundVolume, files) =>
        {
            try
            {
                // List of supported audio formats
                var supportedAudioFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".3gp", ".aa", ".aac", ".aax", ".act", ".aiff", ".alac", ".amr", ".ape",
                    ".au", ".awb", ".dss", ".dvf", ".flac", ".gsm", ".iklax", ".ivs", ".m4a",
                    ".m4b", ".m4p", ".mmf", ".movpkg", ".mp3", ".mpc", ".msv", ".nmf", ".ogg",
                    ".oga", ".mogg", ".opus", ".ra", ".rm", ".raw", ".rf64", ".sln", ".tta",
                    ".voc", ".vox", ".wav", ".wma", ".wv", ".webm", ".8svx", ".cda"
                };

                var pacFiles = files.Where(file => file.EndsWith(".pac", StringComparison.OrdinalIgnoreCase)).ToList();
                var audioFiles = files.Where(file => supportedAudioFormats.Contains(Path.GetExtension(file))).ToList();

                if (pacFiles.Count == 0)
                {
                    printCommandHelp(rootCommand);
                    Console.WriteLine("No .pac files provided. At least one is required.");
                    return;
                }

                if (audioFiles.Count == 0)
                {
                    printCommandHelp(rootCommand);
                    Console.WriteLine("No .audio files provided. At least one is required.");
                    return;
                }


                while (audioFiles.Count < pacFiles.Count)
                {
                    audioFiles.Add(audioFiles[^1]);
                }

                var logBuilder = new StringBuilder();

                logBuilder.AppendLine($"Track Volume: {trackVolume}");
                logBuilder.AppendLine($"Sound Volume: {soundVolume}");
                logBuilder.AppendLine("Processing files...");

                for (int i = 0; i < pacFiles.Count; i++)
                {
                    var audioFile = audioFiles[i];
                    var pacFile = pacFiles[i];

                    string audioFileName = Path.GetFileName(audioFile);
                    string pacFileName = Path.GetFileName(pacFile);
                    string xsbName = $"{Path.GetFileNameWithoutExtension(pacFileName)}.xsb";
                    string xwbName = $"{Path.GetFileNameWithoutExtension(pacFileName)}.xwb";

                    logBuilder.AppendLine($"PAC File: {pacFileName} | Audio File: {audioFileName}");

                    if (!File.Exists(audioFile))
                    {
                        printCommandHelp(rootCommand);
                        Console.WriteLine($"Invalid or missing audio file: {audioFile}");
                        continue;
                    }

                    if (!File.Exists(pacFile))
                    {
                        printCommandHelp(rootCommand);
                        Console.WriteLine($"Invalid or missing .pac file: {pacFile}");
                        continue;
                    }

                    PacFile? toReplace;
                    PacHeader header = new PacHeader(pacFile);

                    XWBCreator xwbCreator = new XWBCreator(Path.GetFileNameWithoutExtension(pacFileName));
                    XSBEditor xsbEditor = new XSBEditor();

                    MemoryStream xwbData = xwbCreator.CreateXWBData(audioFile);

                    toReplace = header.Files.FirstOrDefault(file =>
                        file.Name.Equals(xwbName, StringComparison.OrdinalIgnoreCase) ||
                        (xwbName.Contains(file.Name, StringComparison.OrdinalIgnoreCase) && !file.Name.EndsWith(".xsb", StringComparison.OrdinalIgnoreCase)));

                    if (toReplace != null)
                    {
                        logBuilder.AppendLine($"Replacing .xwb for file {pacFileName}...");
                        header.Replace(toReplace, xwbData);
                        logBuilder.AppendLine($"Finished replacing .xwb for file {pacFileName}.");
                    }

                    MemoryStream xsbData = header.ExtractXSB();

                    using (xsbData)
                    {
                        logBuilder.AppendLine($"Writing {xsbName} sound byte...");
                        xsbEditor.WriteSound(xsbData, soundVolume);
                        logBuilder.AppendLine($"Writing {xsbName} track byte...");
                        xsbEditor.WriteTrack(xsbData, trackVolume);
                        logBuilder.AppendLine($"Recalcuting {xsbName} checksum...");
                        xsbEditor.CalculateChecksum(xsbData);
                        logBuilder.AppendLine($"Finished modifying {xsbName}.");

                        toReplace = header.Files.FirstOrDefault(file =>
                        file.Name.EndsWith(".xsb", StringComparison.OrdinalIgnoreCase) &&
                        file.Name.Contains(Path.GetFileNameWithoutExtension(pacFileName), StringComparison.OrdinalIgnoreCase));

                        if (toReplace != null)
                        {
                            logBuilder.AppendLine($"Replacing .xsb for {pacFileName}...");
                            header.Replace(toReplace, xsbData);
                            logBuilder.AppendLine($"Finished replacing .xsb for {pacFileName}.");
                        }

                    }

                    Console.Write(logBuilder.ToString());

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");

                Console.WriteLine("Stack Trace:");
                Console.WriteLine(ex.StackTrace);

                Console.WriteLine($"Exception Type: {ex.GetType().FullName}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner Exception:");
                    Console.WriteLine(ex.InnerException);
                }
            }

            Console.WriteLine("Processing completed.");


        }, trackVolumeOption, soundVolumeOption, fileArgument);

            try
            {
                rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
