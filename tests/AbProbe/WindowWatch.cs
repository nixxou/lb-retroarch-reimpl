using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AbProbe;

// Chien de garde des fenetres : toutes les 300 ms, cherche une fenetre visible appartenant au processus de la sonde
// (boite modale de LaunchBox, MessageBox). S'il en trouve une, il le dit et arrete la sonde (code 3) : un run non
// surveille ne doit jamais rester bloque sur une fenetre.
public static class WindowWatch
{
    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    public static int Seen { get; private set; }

    public static void Start()
    {
        uint self = (uint)Environment.ProcessId;
        IntPtr console = GetConsoleWindow();
        new Thread(() =>
        {
            while (true)
            {
                var found = new List<string>();
                EnumWindows((hwnd, _) =>
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == self && hwnd != console && IsWindowVisible(hwnd))
                    {
                        var title = new StringBuilder(512);
                        GetWindowText(hwnd, title, title.Capacity);
                        found.Add(title.ToString());
                    }

                    return true;
                }, IntPtr.Zero);

                if (found.Count > 0)
                {
                    Seen += found.Count;
                    Console.Error.WriteLine("FENETRE OUVERTE PAR LA SONDE : " + string.Join(" | ", found) + " -- arret du run.");
                    Console.Out.Flush();
                    Environment.Exit(3);
                }

                Thread.Sleep(300);
            }
        }) { IsBackground = true, Name = "WindowWatch" }.Start();
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
}
