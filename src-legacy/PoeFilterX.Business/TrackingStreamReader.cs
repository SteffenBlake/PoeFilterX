using System.Text;

namespace PoeFilterX.Business
{
    public class TrackingStreamReader : StreamReader
    {
        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Stream stream) : base(stream)
        {
            Path = path;
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path) : base(path)
        {
            Path = path;
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, FileStreamOptions options) : base(path, options)
        {
            Path = path;
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, bool detectEncodingFromByteOrderMarks) : base(path, detectEncodingFromByteOrderMarks)
        {
            Path = path;
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding) : base(path, encoding)
        {
            Path = path;
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks) : base(path, encoding, detectEncodingFromByteOrderMarks)
        {
            Path = path;
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize) : base(path, encoding, detectEncodingFromByteOrderMarks, bufferSize)
        {
            Path = path;
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks, FileStreamOptions options) : base(path, encoding, detectEncodingFromByteOrderMarks, options)
        {
            Path = path;
        }

        public int Line { get; set; } = 1;
        public string Path { get; set; }

        /// <inheritdoc cref="StreamReader.Read()"/>
        public override int Read()
        {
            var character = base.Read();
            if ((char)character == '\n')
            {
                Line++;
            }
            return character;
        }

        /// <inheritdoc cref="StreamReader.Read(char[], int, int)"/>
        public override int Read(char[] buffer, int index, int count)
        {
            var output = base.Read(buffer, index, count);
            Line += buffer.Count(c => c == '\n');
            return output;
        }

        /// <inheritdoc cref="StreamReader.ReadLine"/>
        public override string? ReadLine()
        {
            var output = base.ReadLine();
            Line++;
            return output;
        }

        /// <inheritdoc cref="StreamReader.ReadLineAsync"/>
        public override Task<string?> ReadLineAsync()
        {
            var output = base.ReadLineAsync();
            Line++;
            return output;
        }
    }
}
