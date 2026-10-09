using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Unbroken.LaunchBox.Properties;
using Unbroken.LaunchBox.Windows;

namespace Nixx.RetroArch.Internal
{
    /// <summary>Dernière version et téléchargement des archives (spec § 6).</summary>
    internal static class RetroArchWeb
    {
        // § 6.1.3 : client unique, réglages par défaut de .NET.
        private static readonly HttpClient Http = new HttpClient();

        internal static string ProgramUrl(string? version) => "https://buildbot.libretro.com/stable/" + version + "/windows/x86_64/RetroArch.7z";

        internal static string CoresUrl(string? version) => "https://buildbot.libretro.com/stable/" + version + "/windows/x86_64/RetroArch_cores.7z";

        /// <summary>Dernière version de RetroArch (§ 6.1).</summary>
        internal static string GetLatestVersion()
        {
            string? version;
            try
            {
                version = FromBuildbot();
            }
            catch
            {
                version = null;
            }

            if (version != null)
            {
                return version;
            }

            return FromGitHub();
        }

        private static string? FromBuildbot()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://buildbot.libretro.com/stable/");
            request.Headers.Add("User-Agent", "request");
            using HttpResponseMessage response = Http.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            const string marker = "<a href=\"/stable/";
            var versions = new List<Version>();
            int position = 0;
            while (true)
            {
                int index = body.IndexOf(marker, position, StringComparison.OrdinalIgnoreCase);
                if (index <= 0)
                {
                    break;
                }

                int start = index + marker.Length;
                int end = body.IndexOf("/\"", start, StringComparison.Ordinal);
                if (end <= start)
                {
                    break;
                }

                versions.Add(Version.Parse(body.Substring(start, end - start)));
                position = end;
            }

            if (versions.Count == 0)
            {
                return null;
            }

            return versions.Max()!.ToString();
        }

        private static string FromGitHub()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/libretro/RetroArch/tags");
            request.Headers.Add("User-Agent", "request");
            using HttpResponseMessage response = Http.Send(request);
            response.EnsureSuccessStatusCode();
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new InvalidOperationException(Strings.ErrorParseRetroArchWebsite);
            }

            JArray tags = JArray.Parse(body);
            if (tags.Count == 0)
            {
                throw new InvalidOperationException(Strings.ErrorParseRetroArchWebsite);
            }

            // Liaison dynamique voulue : elle donne les exceptions du tableau de § 6.1.
            dynamic first = tags[0];
            string name = first.name.Value;
            return name.TrimStart('v');
        }

        /// <summary>
        /// Téléchargement d'une archive (§ 6.2). Rend le chemin de l'archive, ou null si annulé.
        /// </summary>
        internal static string? DownloadArchive(string url, Action<long, long> progress, Func<bool> shouldCancel)
        {
            // Avant toute requête et hors de l'interception : crée <racine>\Metadata\Temp, ses erreurs sortent brutes.
            string file = Paths.GetTempFilePath() + ".7z";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage response = Http.Send(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1;
                bool cancelled = false;
                using (Stream source = response.Content.ReadAsStream())
                using (var target = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long done = 0;
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (shouldCancel())
                        {
                            cancelled = true;
                            break;
                        }

                        target.Write(buffer, 0, read);
                        done += read;
                        progress(done, total);
                    }
                }

                if (cancelled)
                {
                    DeleteQuietly(file);
                    return null;
                }

                return file;
            }
            catch (OperationCanceledException)
            {
                DeleteQuietly(file);
                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(Strings.ErrorUnableToDownloadRetroArch, ex);
            }
        }

        private static void DeleteQuietly(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
            }
        }
    }
}
