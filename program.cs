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