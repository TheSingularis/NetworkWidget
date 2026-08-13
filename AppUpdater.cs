using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace NetworkWidget
{
    internal static class AppUpdater
    {
        private const string RepoUrl = "https://github.com/TheSingularis/NetworkWidget";

        // onUpdateApplying, if given, is awaited (e.g. to show a tray notification)
        // after the new version has finished downloading but before the restart that
        // actually swaps it in - the process exits inside ApplyUpdatesAndRestart, so
        // this is the last chance to tell the user anything.
        public static async Task CheckAndApplyAsync(Func<string, Task>? onUpdateApplying = null)
        {
            try
            {
                var source = new GithubSource(RepoUrl, accessToken: null, prerelease: false);
                var mgr = new UpdateManager(source);

                // Not running from a Velopack-managed install (e.g. a dev build launched
                // straight from bin\Release) - there's nothing sensible to update in place.
                if (!mgr.IsInstalled) return;

                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null) return;

                await mgr.DownloadUpdatesAsync(updateInfo);

                if (onUpdateApplying != null)
                {
                    await onUpdateApplying(updateInfo.TargetFullRelease.Version.ToString());
                }

                mgr.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
            }
            catch
            {
                // Best-effort: a network hiccup or GitHub rate limit should never affect
                // normal app operation.
            }
        }
    }
}
