using System.Text;

namespace PoeFilterX.Business
{
    public class TrackingStreamReader : IDisposable
    {
        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Stream stream)
        {
            Path = path;
            Component = new StreamReader(stream);
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path)
        {
            Path = path;
            Component = new StreamReader(path);
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, FileStreamOptions options)
        {
            Path = path;
            Component = new StreamReader(path, options);
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, bool detectEncodingFromByteOrderMarks)
        {
            Path = path;
            Component = new StreamReader(path, detectEncodingFromByteOrderMarks);
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding)
        {
            Path = path;
            Component = new StreamReader(path, encoding);
        }


        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks)
        {
            Path = path;
            Component = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks);
        }


        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize)
        {
            Path = path;
            Component = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks, bufferSize);
        }

        /// <inheritdoc cref="StreamReader"/>
        public TrackingStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks, FileStreamOptions options)
        {
            Path = path;
            Component = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks, options);
        }

        public int Line { get; set; } = 1;
        public string Path { get; set; }

        private StreamReader Component { get; }

        /// <inheritdoc cref="StreamReader.EndOfStream"/>
        public bool EndOfStream => Component.EndOfStream;

        /// <inheritdoc cref="StreamReader.Peek"/>
        public int Peek() => Component.Peek();

        /// <inheritdoc cref="StreamReader.Read()"/>
        public int Read()
        {
            var character = Component.Read();
            if ((char)character == '\n')
            {
                Line++;
            }
            return character;
        }

        /// <inheritdoc cref="StreamReader.Read(char[], int, int)"/>
        public int Read(char[] buffer, int index, int count)
        {
            var output = Component.Read(buffer, index, count);
            Line += buffer.Count(c => c == '\n');
            return output;
        }

        /// <inheritdoc cref="StreamReader.ReadLine"/>
        public string? ReadLine()
        {
            var output = Component.ReadLine();
            Line++;
            return output;
        }

        /// <inheritdoc cref="StreamReader.ReadLineAsync"/>
        public Task<string?> ReadLineAsync()
        {
            var output = Component.ReadLineAsync();
            Line++;
            return output;
        }

        /// <inheritdoc cref="StreamReader.Dispose"/>
        public void Dispose()
        {
            Component.Dispose();
        }
    }
}
