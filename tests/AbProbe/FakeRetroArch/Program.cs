using System;
using System.IO;

// Ecrit out.bin sur stdout et err.bin sur stderr (octets bruts), et trace l'appel dans calls.log.
internal static class Program
{
    private static int Main(string[] args)
    {
        string dir = AppContext.BaseDirectory;
        File.AppendAllText(Path.Combine(dir, "calls.log"),
            "args=[" + string.Join("|", args) + "] cwd=" + Environment.CurrentDirectory + "\r\n");
        Replay(Path.Combine(dir, "out.bin"), Console.OpenStandardOutput());
        Replay(Path.Combine(dir, "err.bin"), Console.OpenStandardError());
        return 0;
    }

    private static void Replay(string path, Stream target)
    {
        if (!File.Exists(path))
        {
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        target.Write(bytes, 0, bytes.Length);
        target.Flush();
    }
}
