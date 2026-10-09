using System.Linq;
using Unbroken.LaunchBox.LocalDb;
using Unbroken.LaunchBox.LocalDb.DbModels;

namespace Nixx.RetroArch.Internal
{
    internal static class LocalDbRetroArch
    {
        /// <summary>Premier enregistrement LocalDb nommé RetroArch, relu à chaque appel.</summary>
        internal static DbEmulator? Get()
        {
            return GamesDb.GetEmulatorsByNameWithPlatformsAsync("RetroArch").GetAwaiter().GetResult().FirstOrDefault();
        }
    }
}
