using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AbProbe;

// Garde reseau de la sonde, branchee sur l'evenement de diagnostic du gestionnaire HTTP de .NET (avant connexion).
//   Block : toute requete est notee puis interrompue (NetworkBlockedException) ; run par defaut.
//   Fake  : la requete est redirigee vers le faux serveur local (http://127.0.0.1:<port>/<hote><chemin>).
//   Real  : la requete part sur le vrai reseau (option --network, accord de Mehdi), elle est seulement notee.
public sealed class NetworkBlockedException : Exception
{
    public NetworkBlockedException(string url)
        : base("sonde : requete reseau bloquee avant connexion")
    {
        Url = url;
    }

    public string Url { get; }
}

public enum NetMode
{
    Block,
    Fake,
    Real,
}

public sealed class NetEvent
{
    public string Request { get; set; }

    public List<string> Headers { get; set; }

    public string Response { get; set; }

    public long ContentLength { get; set; } = -1;
}

public sealed class NetGuard : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>
{
    private static readonly NetGuard Instance = new();
    private static readonly object Gate = new();
    private static readonly List<NetEvent> Seen = new();
    private static readonly ConditionalWeakTable<HttpRequestMessage, NetEvent> Handled = new();
    private static readonly HashSet<string> IgnoredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "traceparent", "tracestate", "Request-Id", "Correlation-Context", "baggage",
    };

    public static bool Armed { get; private set; }

    public static string SelfTest { get; private set; } = "non fait";

    public static NetMode Mode { get; set; } = NetMode.Block;

    public static Uri FakeBase { get; set; }

    public static void Install(bool network, FakeServer server)
    {
        if (!network)
        {
            // Run par defaut : proxy de secours sur un port ferme, puis auto-test du blocage.
            HttpClient.DefaultProxy = new WebProxy("http://127.0.0.1:9");
        }

        DiagnosticListener.AllListeners.Subscribe(Instance);
        Take();
        if (!network)
        {
            Mode = NetMode.Block;
            try
            {
                using var client = new HttpClient();
                client.GetAsync("https://buildbot.libretro.com/stable/").GetAwaiter().GetResult();
                SelfTest = "ECHEC : la requete n'a pas ete bloquee";
            }
            catch (Exception ex)
            {
                bool blocked = false;
                for (Exception e = ex; e != null; e = e.InnerException)
                {
                    blocked |= e is NetworkBlockedException;
                }

                Armed = blocked && Take().Count == 1;
                SelfTest = Armed ? "OK : requete bloquee avant connexion" : "ECHEC : " + ex.GetType().FullName;
            }

            return;
        }

        // Mode --network : l'auto-test verifie la redirection vers le faux serveur (hote .invalid, jamais resolu).
        Mode = NetMode.Fake;
        FakeBase = new Uri($"http://127.0.0.1:{server.Port}/");
        server.Handler = r => r.Path == "/selftest.invalid/ping" ? FakeResponse.Text(200, "pong") : null;
        try
        {
            using var client = new HttpClient();
            string body = client.GetStringAsync("https://selftest.invalid/ping").GetAwaiter().GetResult();
            Armed = body == "pong" && Take().Count == 1;
            SelfTest = Armed ? "OK : requetes redirigees vers le faux serveur local" : "ECHEC : reponse inattendue";
        }
        catch (Exception ex)
        {
            Armed = false;
            SelfTest = "ECHEC : " + ex.GetType().FullName + " " + ex.Message;
        }

        server.TakeLog();
        server.Handler = null;
        Mode = NetMode.Block;
    }

    // Rend et vide la liste des requetes vues.
    public static List<NetEvent> Take()
    {
        lock (Gate)
        {
            var copy = new List<NetEvent>(Seen);
            Seen.Clear();
            return copy;
        }
    }

    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == "HttpHandlerDiagnosticListener")
        {
            value.Subscribe(this, (name, a, b) => true);
        }
    }

    public void OnNext(KeyValuePair<string, object> value)
    {
        object payload = value.Value;
        var request = payload?.GetType().GetProperty("Request")?.GetValue(payload) as HttpRequestMessage;
        if (request == null)
        {
            return;
        }

        if (value.Key == "System.Net.Http.HttpRequestOut.Start" || value.Key == "System.Net.Http.Request")
        {
            lock (Gate)
            {
                if (Handled.TryGetValue(request, out _))
                {
                    return;
                }

                var ev = new NetEvent
                {
                    Request = request.Method + " " + request.RequestUri,
                    Headers = request.Headers.Where(h => !IgnoredHeaders.Contains(h.Key))
                        .Select(h => h.Key + ": " + string.Join(",", h.Value)).OrderBy(h => h, StringComparer.Ordinal).ToList(),
                };
                Handled.Add(request, ev);
                Seen.Add(ev);
            }

            switch (Mode)
            {
                case NetMode.Block:
                    throw new NetworkBlockedException(request.Method + " " + request.RequestUri);
                case NetMode.Fake:
                    var target = new Uri(FakeBase, "/" + request.RequestUri.Host + request.RequestUri.PathAndQuery);
                    SetUri(request, target);
                    if (request.RequestUri != target)
                    {
                        throw new NetworkBlockedException("redirection impossible : " + target);
                    }

                    break;
            }

            return;
        }

        if (value.Key == "System.Net.Http.HttpRequestOut.Stop" || value.Key == "System.Net.Http.Response")
        {
            var response = payload.GetType().GetProperty("Response")?.GetValue(payload) as HttpResponseMessage;
            lock (Gate)
            {
                if (response != null && Handled.TryGetValue(request, out NetEvent ev) && ev.Response == null)
                {
                    long length = response.Content?.Headers.ContentLength ?? -1;
                    ev.ContentLength = length;
                    ev.Response = $"{(int)response.StatusCode} length={length}"
                        + $" etag={response.Headers.ETag?.Tag ?? "-"}"
                        + $" last-modified={response.Content?.Headers.LastModified?.ToString("u") ?? "-"}";
                }
            }
        }
    }

    private static void SetUri(HttpRequestMessage request, Uri target)
    {
        try
        {
            request.RequestUri = target;
        }
        catch (InvalidOperationException)
        {
            typeof(HttpRequestMessage).GetField("_requestUri", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(request, target);
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
