using System.Text;

namespace MusicModder.Services
{
    public class PacHeader
    {
        private readonly string _path;
        private string MagicWord {  get; set; }
        private int StartOffset { get; set; }
        private int Size { get; set; }
        private int FileCount { get; set; }
        public int bufferSize { get; set; }
        private int NameLength { get; set; }
        public List<PacFile> Files {  get; set; }

        public PacHeader(string pacPath)
        {
            _path = pacPath;
            bufferSize = 81920;

            using (var fileStream = new FileStream(_path, FileMode.Open, FileAccess.Read))
            using (BinaryReader binaryFile = new BinaryReader(fileStream))
            {


                string magicWord = new string(binaryFile.ReadChars(4));

                if (!magicWord.Equals("FPAC")) { 
                
                    throw new PacHeaderException("File has an incorrect structure.");
                }


                MagicWord = magicWord;

                StartOffset = binaryFile.ReadInt32();

                Size = binaryFile.ReadInt32();

                FileCount = binaryFile.ReadInt32();

                Files = new List<PacFile>();

                binaryFile.BaseStream.Seek(4, SeekOrigin.Current);

                NameLength = binaryFile.ReadInt32();

                binaryFile.BaseStream.Seek(8, SeekOrigin.Current);

                for (int i = 0; i < FileCount; i++)
                {
                    string name = new string(binaryFile.ReadChars(NameLength)).Replace("\0", "");
                    int id = binaryFile.ReadInt32();
                    int offset = (binaryFile.ReadInt32() + StartOffset + 15) / 16 * 16;
                    int size = binaryFile.ReadInt32();

                    binaryFile.BaseStream.Seek(4 - (NameLength % 4), SeekOrigin.Current);   
                    PacFile fileObj = new PacFile(id, name, offset, size);

                    Files.Add(fileObj);
                }

            }

        }

        public void Replace(PacFile toReplace, Stream replacementFileStream)
        {

            int fileSize = (int)replacementFileStream.Length;

            using (var fileStream = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite))
            {

                PacFile? match = Files.FirstOrDefault(file => file == toReplace);

                if (match != null)
                {
                    match.Size = fileSize;
                }
                else
                {
                    new PacHeaderException($"Error: No matching file found for Id: {toReplace.Id}");
                    return;
                }

                RecalculateValues();

                using (MemoryStream tempStream = new MemoryStream())
                using (replacementFileStream)
                {
                    // Ensure we start from the beginning of the replacement file
                    replacementFileStream.Seek(0, SeekOrigin.Begin);
                    // Create a BinaryWriter to write to the temporary memory stream
                    using (BinaryWriter writer = new BinaryWriter(tempStream, Encoding.ASCII))
                    {
                        writer.Write(MagicWord.ToCharArray());
                        writer.Write(StartOffset);
                        writer.Write(Size);
                        writer.Write(Files.Count);
                        writer.Write(1);
                        writer.Write(NameLength);
                        writer.Write(0L); // Writing long (64-bit integer)

                        foreach (PacFile file in Files)
                        {
                            writer.Write(file.Name.ToCharArray());

                            for (int i = 0; i < NameLength - file.Name.Length; i++)
                            {
                                writer.Write((byte)0);
                            }

                            writer.Write(file.Id);
                            writer.Write(file.Offset - StartOffset);
                            writer.Write(file.Size);
                            writer.Write(0);
                            // align to 16 byte boundary
                            while (writer.BaseStream.Position % 16 != 0L)
                            {
                                writer.Write((byte)0);
                            }
                        }

                        foreach (PacFile file in Files)
                        {
                            if (file == toReplace)
                            {
                                int bytesWritten = 0;
                                int bytesRead;

                                byte[] buffer = new byte[bufferSize];
                                // Read the replacement file in chunks and write to the temporary file
                                while ((bytesRead = replacementFileStream.Read(buffer)) > 0)
                                {
                                    tempStream.Write(buffer, 0, bytesRead);
                                    bytesWritten += bytesRead;
                                }

                                // Zero out remaining space if the replacement file is smaller
                                int remainingSize = file.Size - bytesWritten;
                                if (remainingSize > 0)
                                {
                                    byte[] zeroBuffer = new byte[remainingSize];
                                    tempStream.Write(zeroBuffer, 0, zeroBuffer.Length);
                                }

                            }
                            else
                            {
                                int bytesRead;
                                int totalBytesRead = 0; // Track the total bytes read
                                byte[] buffer = new byte[bufferSize];
                                fileStream.Seek(file.Offset, SeekOrigin.Begin);
                                // Read in chunks
                                while (totalBytesRead < file.Size &&
                                       (bytesRead = fileStream.Read(buffer, 0, Math.Min(buffer.Length, file.Size - totalBytesRead))) > 0)
                                {
                                    tempStream.Write(buffer, 0, bytesRead);
                                    totalBytesRead += bytesRead;
                                }

                            }

                            // align to 16 byte boundary
                            while (writer.BaseStream.Position % 16 != 0L)
                            {
                                writer.Write((byte)0);
                            }
                        }

                        fileStream.SetLength(0); // enure data is overwritten
                        tempStream.Seek(0, SeekOrigin.Begin);
                        fileStream.Seek(0, SeekOrigin.Begin);

                        byte[] writeBuffer = new byte[bufferSize];
                        int bytesReadFrom;
                        while ((bytesReadFrom = tempStream.Read(writeBuffer, 0, writeBuffer.Length)) > 0)
                        {
                            fileStream.Write(writeBuffer, 0, bytesReadFrom);
                        }

                    }

                }
            }
        }

        public void RecalculateValues()
        {
            int longestName = Files.Aggregate(0, (max, cur) => max > cur.Name.Length ? max : cur.Name.Length);

            NameLength = (int)((longestName + 4) / 4) * 4;
            int headerEntrySize = NameLength + 16;
            headerEntrySize = (int)((headerEntrySize + 15) / 16) * 16;

            int headerSize = 32 + Files.Count * headerEntrySize;

            StartOffset = (int)((headerSize + 15) / 16) * 16;
            Files[0].Offset = StartOffset;

            for (int i = 1; i < Files.Count; i++)
            {
                PacFile previousFile = Files[i - 1];
                PacFile currentFile = Files[i];
                // Recalculate offset for the current file and reassign
                int newOffset = previousFile.Offset + previousFile.Size;
                newOffset = (int)((newOffset + 15) / 16) * 16;
                currentFile.Offset = newOffset;
            }

            PacFile lastFile = Files[Files.Count - 1];
            Size = lastFile.Offset + lastFile.Size;

            while (Size % 16 != 0) { 
                Size++;
            }
        }

        public MemoryStream ExtractXSB()
        {
            using (var fileStream = new FileStream(_path, FileMode.Open, FileAccess.Read))
            {
                PacFile? file = Files.FirstOrDefault(file => file.Name.EndsWith(".xsb", StringComparison.OrdinalIgnoreCase));

                if (file == null)
                {
                    throw new PacHeaderException($"Could not find an .xsb in the file {Path.GetFileName(_path)}");
                }

                MemoryStream output = new MemoryStream();

                int remainingBytes = file.Size;
                fileStream.Seek(file.Offset, SeekOrigin.Begin);

                byte[] buffer = new byte[bufferSize];

                while (remainingBytes > 0)
                {
                    int bytesRead = fileStream.Read(buffer, 0, Math.Min(remainingBytes, bufferSize));

                    if (bytesRead > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                        remainingBytes -= bytesRead;
                    }
                    else
                    {
                        throw new IOException("Unexpected end of file while extracting.");
                    }
                }

                return output;

            }

        }


        public class PacHeaderException(string message) : Exception(message)
        {
        }
    }
}
