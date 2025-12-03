using System;
using System. IO;
using System. Text;
using System.Text.Json;

namespace Contracts.IPC
{
    /// <summary>
    /// Handles serialization and framing of IPC messages.
    /// 
    /// Wire format:
    /// [4 bytes: length prefix (Int32)] [N bytes: UTF-8 JSON payload]
    /// </summary>
    public static class IpcProtocol
    {
        /// <summary>
        /// Maximum allowed message size (1 MB).
        /// </summary>
        public const int MaxMessageSize = 1024 * 1024;

        /// <summary>
        /// Minimum valid message size (empty JSON object: {}). 
        /// </summary>
        public const int MinMessageSize = 2;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy. CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Writes a message to the stream with length prefix framing.
        /// </summary>
        /// <param name="stream">Stream to write to. </param>
        /// <param name="message">Message to send.</param>
        /// <exception cref="ArgumentNullException">If stream or message is null.</exception>
        /// <exception cref="InvalidOperationException">If message is too large. </exception>
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
                string json = JsonSerializer. Serialize(message, JsonOptions);
                byte[] jsonBytes = Encoding. UTF8.GetBytes(json);

                // Validate size
                if (jsonBytes.Length > MaxMessageSize)
                {
                    throw new InvalidOperationException(
                        "Message size (" + jsonBytes. Length + " bytes) exceeds maximum (" + MaxMessageSize + " bytes)");
                }

                // Write length prefix (4 bytes, little-endian)
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
        /// Reads a message from the stream. 
        /// </summary>
        /// <param name="stream">Stream to read from. </param>
        /// <returns>The deserialized message, or null if stream ended or error occurred.</returns>
        public static IpcMessage ReadMessage(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                return null;

            try
            {
                // Read length prefix (4 bytes)
                byte[] lengthBytes = ReadExactBytes(stream, 4);
                if (lengthBytes == null)
                    return null; // End of stream

                int length = BitConverter. ToInt32(lengthBytes, 0);

                // Validate length
                if (length < MinMessageSize)
                {
                    // Invalid message - skip it
                    return null;
                }

                if (length > MaxMessageSize)
                {
                    // Message too large - protocol error
                    throw new InvalidDataException(
                        "Message size (" + length + " bytes) exceeds maximum (" + MaxMessageSize + " bytes)");
                }

                // Read JSON payload
                byte[] jsonBytes = ReadExactBytes(stream, length);
                if (jsonBytes == null)
                    return null; // End of stream

                string json = Encoding. UTF8.GetString(jsonBytes);

                // Validate JSON is not empty
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
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>Byte array, or null if end of stream reached before reading all bytes.</returns>
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

        /// <summary>
        /// Attempts to write a message, returning success/failure instead of throwing.
        /// </summary>
        /// <param name="stream">Stream to write to.</param>
        /// <param name="message">Message to send.</param>
        /// <returns>True if message was sent successfully, false otherwise. </returns>
        public static bool TryWriteMessage(Stream stream, IpcMessage message)
        {
            try
            {
                if (stream == null || message == null || !stream.CanWrite)
                    return false;

                WriteMessage(stream, message);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to read a message, returning success/failure via out parameter.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="message">The read message, or null if failed.</param>
        /// <returns>True if a message was read successfully, false otherwise.</returns>
        public static bool TryReadMessage(Stream stream, out IpcMessage message)
        {
            try
            {
                message = ReadMessage(stream);
                return message != null;
            }
            catch
            {
                message = null;
                return false;
            }
        }
    }
}