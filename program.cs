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
        // You can change this port if needed
        private const int Port = 8080;

        // Only these extensions are allowed
        private static readonly string[] AllowedExtensions = { ".html", ".css", ".js" };

        // Map extension → MIME type
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
            // Determine absolute path to the webroot folder
            // (and normalize it so there’s no trailing slash)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string webRoot = Path.GetFullPath(Path.Combine(baseDir, "webroot")).TrimEnd(Path.DirectorySeparatorChar);

            if (!Directory.Exists(webRoot))
            {
                Console.WriteLine($"Error: “webroot” folder not found at '{webRoot}'.");
                Console.WriteLine("Create a folder named “webroot” next to the executable and place your .html/.css/.js files inside.");
                return;
            }

            TcpListener listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine($"[+] Server started on port {Port}. Serving from: {webRoot}\n");

            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    // Spawn a thread to handle this one connection
                    Thread thread = new Thread(() => HandleClient(client, webRoot));
                    thread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Listener exception: {ex.Message}");
                }
            }
        }

        private static void HandleClient(TcpClient client, string webRoot)
        {
            using (client)
            {
                NetworkStream netStream = client.GetStream();
                StreamReader reader = new StreamReader(netStream, Encoding.UTF8, false, 8192, leaveOpen: true);
                StreamWriter writer = new StreamWriter(netStream, Encoding.UTF8, 8192, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };

                try
                {
                    // 1) Read the request line, e.g. "GET /index.html HTTP/1.1"
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(requestLine))
                    {
                        // Malformed or empty—treat as bad request
                        SendBadRequest(writer);
                        return;
                    }

                    // 2) Consume remaining headers until blank line
                    string headerLine;
                    while (!string.IsNullOrWhiteSpace(headerLine = reader.ReadLine()))
                    {
                        // no-op; we just skip them
                    }
