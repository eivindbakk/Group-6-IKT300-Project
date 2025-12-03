using System;
using System. IO;
using System.Text;
using System.Text.Json;

namespace Contracts.IPC
{
    /// <summary>
    /// Handles serialization and framing of IPC messages. 
    /// 
    /// Wire format (length-prefixed framing):
    /// [4 bytes: length prefix (Int32)] [N bytes: UTF-8 JSON payload]
    /// 
    /// This ensures we can read complete messages even if TCP/pipe
    /// delivers data in chunks.
    /// </summary>
    public static class IpcProtocol
    {
        /// <summary>
        /// Maximum allowed message size (1 MB) to prevent memory exhaustion. 
        /// </summary>
        public const int MaxMessageSize = 1024 * 1024;

        /// <summary>
        /// Minimum valid message size (empty JSON object: {}). 
        /// </summary>
        public const int MinMessageSize = 2;

        // JSON serialization options for consistent formatting
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Writes a message to the stream with length prefix framing.
        /// Format: [4-byte length][JSON bytes]
        /// </summary>
        public static void WriteMessage(Stream stream, IpcMessage message)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (! stream.CanWrite)
                return;

            try
            {
                // Serialize message to JSON
                string json = JsonSerializer. Serialize(message, JsonOptions);
                byte[] jsonBytes = Encoding. UTF8.GetBytes(json);

                // Validate size to prevent oversized messages
                if (jsonBytes.Length > MaxMessageSize)
                {
                    throw new InvalidOperationException(
                        "Message size (" + jsonBytes. Length + " bytes) exceeds maximum (" + MaxMessageSize + " bytes)");
                }

                // Write 4-byte length prefix (little-endian)
                byte[] lengthBytes = BitConverter.GetBytes(jsonBytes. Length);

                stream.Write(lengthBytes, 0, 4);
                stream.Write(jsonBytes, 0, jsonBytes.Length);
                stream.Flush();
            }
            catch (IOException)
            {
                // Pipe broken - ignore silently
            }
            catch (ObjectDisposedException)
            {
                // Stream disposed - ignore silently
            }
        }

        /// <summary>
        /// Reads a message from the stream using length-prefix framing.
        /// Returns null if stream ended or on error.
        /// </summary>
        public static IpcMessage ReadMessage(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream. CanRead)
                return null;

            try
            {
                // Read 4-byte length prefix
                byte[] lengthBytes = ReadExactBytes(stream, 4);
                if (lengthBytes == null)
                    return null; // End of stream

                int length = BitConverter. ToInt32(lengthBytes, 0);

                // Validate length bounds
                if (length < MinMessageSize)
                {
                    return null;
                }

                if (length > MaxMessageSize)
                {
                    throw new InvalidDataException(
                        "Message size (" + length + " bytes) exceeds maximum (" + MaxMessageSize + " bytes)");
                }

                // Read the JSON payload
                byte[] jsonBytes = ReadExactBytes(stream, length);
                if (jsonBytes == null)
                    return null;

                string json = Encoding. UTF8.GetString(jsonBytes);

                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonSerializer. Deserialize<IpcMessage>(json, JsonOptions);
            }
            catch (IOException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (JsonException)
            {
                // Invalid JSON - return null rather than crashing
                return null;
            }
        }

        /// <summary>
        /// Reads exactly the specified number of bytes from the stream.
        /// Handles partial reads by looping until all bytes are read. 
        /// Returns null if end of stream reached.
        /// </summary>
        private static byte[] ReadExactBytes(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;

            while (totalRead < count)
            {
                int read = stream.Read(buffer, totalRead, count - totalRead);
                if (read == 0)
                    return null; // End of stream

                totalRead += read;
            }

            return buffer;
        }
    }
}