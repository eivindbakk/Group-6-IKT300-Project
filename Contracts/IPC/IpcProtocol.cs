using System;
using System.IO;
using System. Text;
using System.Text.Json;

namespace Contracts.IPC
{
    public static class IpcProtocol
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy. CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static void WriteMessage(Stream stream, IpcMessage message)
        {
            if (stream == null || ! stream.CanWrite)
                return;

            try
            {
                string json = JsonSerializer. Serialize(message, JsonOptions);
                byte[] jsonBytes = Encoding. UTF8.GetBytes(json);
                byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

                stream. Write(lengthBytes, 0, 4);
                stream.Write(jsonBytes, 0, jsonBytes.Length);
                stream.Flush();
            }
            catch (IOException)
            {
                // Pipe broken - ignore
            }
            catch (ObjectDisposedException)
            {
                // Stream disposed - ignore
            }
        }

        public static IpcMessage ReadMessage(Stream stream)
        {
            if (stream == null || !stream. CanRead)
                return null;

            try
            {
                // Check if data is available (non-blocking check)
                if (stream is System.IO.Pipes.PipeStream pipeStream)
                {
                    // For pipes, we need to try reading
                }

                // Read length prefix (4 bytes)
                byte[] lengthBytes = new byte[4];
                int totalRead = 0;
                
                while (totalRead < 4)
                {
                    int read = stream.Read(lengthBytes, totalRead, 4 - totalRead);
                    if (read == 0)
                        return null; // End of stream
                    totalRead += read;
                }

                int length = BitConverter. ToInt32(lengthBytes, 0);
                
                if (length <= 0 || length > 1024 * 1024) // Max 1MB
                    return null;

                // Read JSON payload
                byte[] jsonBytes = new byte[length];
                totalRead = 0;
                
                while (totalRead < length)
                {
                    int read = stream.Read(jsonBytes, totalRead, length - totalRead);
                    if (read == 0)
                        return null; // End of stream
                    totalRead += read;
                }

                string json = Encoding. UTF8.GetString(jsonBytes);
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
                return null;
            }
        }
    }
}