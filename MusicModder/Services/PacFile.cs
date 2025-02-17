namespace MusicModder.Services
{
    public class PacFile
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }

        public PacFile(int id, string name, int offset, int size)
        {
            Id = id;
            Name = name;
            Offset = offset;
            Size = size;
        }

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj is not PacFile)
                return false;
            else
                return this.Id == ((PacFile)obj).Id;
        }

    }
}
