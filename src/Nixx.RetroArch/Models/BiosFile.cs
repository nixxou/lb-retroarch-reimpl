using Unbroken.LaunchBox.Plugins;

namespace Nixx.RetroArch.Models
{
    /// <summary>Fichier BIOS dont l'emplacement est le dossier « system » courant (spec § 1).</summary>
    public class BiosFile : EmulatorBiosFile
    {
        /// <summary>Global au processus, réécrit par GetBiosFilesForPlatform.</summary>
        public static string SystemFolder { get; set; } = "system";

        public BiosFile(string fileName, bool required, string? description = null, string? md5 = null, EmulatorBiosGroup? group = null)
            : base(SystemFolder, fileName, required, description, md5, group)
        {
        }
    }
}
