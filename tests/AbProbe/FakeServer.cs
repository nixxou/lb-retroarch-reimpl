using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AbProbe;

// Faux serveur HTTP/1.1 sur la boucle locale (mode --network, cas « faux serveur » de la section 13).
// Les requetes y arrivent redirigees par NetGuard : chemin = /<hote d'origine><chemin d'origine>.
public sealed class FakeRequest
{
    public string Method { get; init; }

    public string Path { get; init; }

    public List<string> Headers { get; init; }
}

public sealed class FakeResponse
{
    public int Status { get; init; } = 200;

    public byte[] Body { get; init; } = Array.Empty<byte>();

    // Attente avant les en-tetes (delai HTTP du client), puis fermeture.
    public int StallBeforeHeadersMs { get; init; }

    // Attente entre deux paquets de 16 Kio du corps (telechargement lent).
    public int DelayPerChunkMs { get; init; }

    public static FakeResponse Text(int status, string body) => new() { Status = status, Body = Encoding.UTF8.GetBytes(body) };
}

public sealed class FakeServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly List<string> log = new();
    private volatile bool stopped;

    public FakeServer()
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        new Thread(AcceptLoop) { IsBackground = true, Name = "FakeServer" }.Start();
    }

    public int Port { get; }

    public Func<FakeRequest, FakeResponse> Handler { get; set; }

    public List<string> TakeLog()
    {
        lock (log)
        {
            var copy = new List<string>(log);
            log.Clear();
            return copy;
        }
    }

    private void AcceptLoop()
    {
        while (!stopped)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (Exception)
            {
                return;
            }

            new Thread(() => Serve(client)) { IsBackground = true }.Start();
        }
    }

    private void Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                var head = new List<byte>();
                while (head.Count < 65536)
                {
                    int b = stream.ReadByte();
                    if (b < 0)
                    {
                        return;
                    }

                    head.Add((byte)b);
                    int n = head.Count;
                    if (n >= 4 && head[n - 4] == '\r' && head[n - 3] == '\n' && head[n - 2] == '\r' && head[n - 1] == '\n')
                    {
                        break;
                    }
                }

                string[] lines = Encoding.ASCII.GetString(head.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                string[] first = lines[0].Split(' ');
                var headers = lines.Skip(1)
                    .Where(l => !l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
                                && !l.StartsWith("traceparent:", StringComparison.OrdinalIgnoreCase)
                                && !l.StartsWith("tracestate:", StringComparison.OrdinalIgnoreCase)
                                && !l.StartsWith("Request-Id:", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(l => l, StringComparer.Ordinal).ToList();
                var request = new FakeRequest { Method = first[0], Path = first[1], Headers = headers };
                lock (log)
                {
                    log.Add(request.Method + " " + request.Path + " [" + string.Join(" | ", headers) + "]");
                }

                FakeResponse response = Handler?.Invoke(request) ?? FakeResponse.Text(404, "not found");
                if (response.StallBeforeHeadersMs > 0)
                {
                    Thread.Sleep(response.StallBeforeHeadersMs);
                    return;
                }

                string reason = response.Status switch { 200 => "OK", 404 => "Not Found", 500 => "Internal Server Error", _ => "Status" };
                byte[] header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {response.Status} {reason}\r\nContent-Length: {response.Body.Length}\r\nConnection: close\r\nContent-Type: application/octet-stream\r\n\r\n");
                stream.Write(header, 0, header.Length);
                for (int offset = 0; offset < response.Body.Length; offset += 16384)
                {
                    int count = Math.Min(16384, response.Body.Length - offset);
                    stream.Write(response.Body, offset, count);
                    stream.Flush();
                    if (response.DelayPerChunkMs > 0)
                    {
                        Thread.Sleep(response.DelayPerChunkMs);
                    }
                }
            }
            catch (IOException)
            {
                // Client parti (annulation) : rien a faire.
            }
        }
    }

    public void Dispose()
    {
        stopped = true;
        listener.Stop();
    }
}
