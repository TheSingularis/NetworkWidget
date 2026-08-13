using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace NetworkWidget
{
    internal static class AppUpdater
    {
        private const string RepoUrl = "https://github.com/TheSingularis/NetworkWidget";

        // onStatus, if given, is called with short human-readable stage names as the
        // check progresses (e.g. for a progress window). onUpdateApplying, if given,
        // is awaited after the new version has finished downloading but before the
        // restart that actually swaps it in - the process exits inside
        // ApplyUpdatesAndRestart, so this is the last chance to tell the user anything.
        public static async Task CheckAndApplyAsync(Action<string>? onStatus = null, Func<string, Task>? onUpdateApplying = null)
        {
            try
            {
                onStatus?.Invoke("Checking for updates...");

                var source = new GithubSource(RepoUrl, accessToken: null, prerelease: false);
                var mgr = new UpdateManager(source);

                // Not running from a Velopack-managed install (e.g. a dev build launched
                // straight from bin\Release) - there's nothing sensible to update in place.
                if (!mgr.IsInstalled)
                {
                    onStatus?.Invoke("Not managed by the updater");
                    return;
                }

                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    onStatus?.Invoke("Up to date");
                    return;
                }

                onStatus?.Invoke("Downloading update...");
                await mgr.DownloadUpdatesAsync(updateInfo);

                onStatus?.Invoke("Installing update...");
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
                onStatus?.Invoke("Couldn't check for updates");
            }
        }
    }
}
