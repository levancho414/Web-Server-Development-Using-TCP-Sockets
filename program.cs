using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SimpleTcpWebServer
{
    class Program
    {
        private const int Port = 8080;
        private static readonly string[] AllowedExtensions = { ".html", ".css", ".js" };

        private static string GetMimeType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".html": return "text/html";
                case ".css": return "text/css";
                case ".js": return "application/javascript";
                default: return "application/octet-stream";
            }
        }

        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string webRoot = Path.GetFullPath(Path.Combine(baseDir, "webroot"))
                                .TrimEnd(Path.DirectorySeparatorChar);

            if (!Directory.Exists(webRoot))
            {
                Console.WriteLine($"Error: 'webroot' folder not found at:\n    {webRoot}");
                Console.WriteLine("Create a folder named 'webroot' next to the executable and add HTML/CSS/JS files.");
                return;
            }

            TcpListener listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine($"[+] Server started on port {Port}.\n    Serving files from: {webRoot}\n");

            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread t = new Thread(() => HandleClient(client, webRoot));
                    t.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Listener error: {ex.Message}");
                }
            }
        }

        private static void HandleClient(TcpClient client, string webRoot)
        {
            using (client)
            {
                NetworkStream netStream = client.GetStream();
                StreamReader reader = new StreamReader(netStream, Encoding.UTF8, false, 8192, true);
                StreamWriter writer = new StreamWriter(netStream, Encoding.UTF8, 8192, true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };

                string clientEndpoint = ((IPEndPoint)client.Client.RemoteEndPoint).ToString();

                try
                {
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(requestLine))
                    {
                        LogRequest(clientEndpoint, "UNKNOWN", "UNKNOWN", 400);
                        SendError(writer, webRoot, 400, "Bad Request");
                        return;
                    }

                    while (!string.IsNullOrWhiteSpace(reader.ReadLine())) { }

                    string[] tokens = requestLine.Split(' ');
                    if (tokens.Length < 3)
                    {
                        LogRequest(clientEndpoint, "UNKNOWN", "UNKNOWN", 400);
                        SendError(writer, webRoot, 400, "Bad Request");
                        return;
                    }

                    string method = tokens[0];
                    string rawUrl = tokens[1];

                    if (!method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        LogRequest(clientEndpoint, method, rawUrl, 405);
                        SendError(writer, webRoot, 405, "Method Not Allowed");
                        return;
                    }

                    string path = rawUrl.Split('?')[0];
                    if (path == "/") path = "/index.html";

                    string relativePath = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    string requestedFile = Path.Combine(webRoot, relativePath);

                    string fullPath;
                    try
                    {
                        fullPath = Path.GetFullPath(requestedFile);
                    }
                    catch
                    {
                        LogRequest(clientEndpoint, method, path, 403);
                        SendError(writer, webRoot, 403, "Forbidden");
                        return;
                    }

                    if (!fullPath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        && !fullPath.Equals(webRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        LogRequest(clientEndpoint, method, path, 403);
                        SendError(writer, webRoot, 403, "Forbidden");
                        return;
                    }

                    string extension = Path.GetExtension(fullPath);
                    if (Array.IndexOf(AllowedExtensions, extension) < 0)
                    {
                        LogRequest(clientEndpoint, method, path, 403);
                        SendError(writer, webRoot, 403, "Forbidden");
                        return;
                    }

                    if (!File.Exists(fullPath))
                    {
                        LogRequest(clientEndpoint, method, path, 404);
                        SendError(writer, webRoot, 404, "Not Found");
                        return;
                    }

                    byte[] fileBytes = File.ReadAllBytes(fullPath);
                    string mimeType = GetMimeType(extension);

                    LogRequest(clientEndpoint, method, path, 200);

                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine($"Content-Type: {mimeType}");
                    writer.WriteLine($"Content-Length: {fileBytes.Length}");
                    writer.WriteLine("Connection: close");
                    writer.WriteLine();
                    writer.Flush();

                    netStream.Write(fileBytes, 0, fileBytes.Length);
                    netStream.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Error: {ex.Message}");
                    LogRequest(clientEndpoint, "UNKNOWN", "UNKNOWN", 500);
                    SendError(writer, webRoot, 500, "Internal Server Error");
                }
            }
        }

        private static void LogRequest(string clientEndpoint, string method, string path, int statusCode)
        {
            try
            {
                string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {clientEndpoint}  \"{method} {path}\"  {statusCode}";
                File.AppendAllText("requests.log", logLine + Environment.NewLine);
            }
            catch { }
        }

        private static void SendError(StreamWriter writer, string webRoot, int statusCode, string statusText)
        {
            string errorFilePath = Path.Combine(webRoot, "error.html");
            byte[] bodyBytes;
            string contentType = "text/html";

            if (File.Exists(errorFilePath))
            {
                bodyBytes = File.ReadAllBytes(errorFilePath);
            }
            else
            {
                string inlineBody = $"<html><head><title>{statusCode} {statusText}</title></head>"
                                  + $"<body><h1>Error {statusCode}: {statusText}</h1></body></html>";
                bodyBytes = Encoding.UTF8.GetBytes(inlineBody);
            }

            writer.WriteLine($"HTTP/1.1 {statusCode} {statusText}");
            writer.WriteLine($"Content-Type: {contentType}");
            writer.WriteLine($"Content-Length: {bodyBytes.Length}");
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Flush();

            try
            {
                writer.BaseStream.Write(bodyBytes, 0, bodyBytes.Length);
            }
            catch { }
        }
    }
}