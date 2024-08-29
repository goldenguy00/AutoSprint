using BepInEx.Logging;
using System.Security.Permissions;
using System.Security;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace AutoSprint
{
    internal static class Log
    {
        private static ManualLogSource _logSource;

        internal static void Init(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        internal static void Debug(string data) => _logSource.LogDebug(data);
        internal static void Error(string data) => _logSource.LogError(data);
        internal static void Fatal(string data) => _logSource.LogFatal(data);
        internal static void Info(string data) => _logSource.LogInfo(data);
        internal static void Message(string data) => _logSource.LogMessage(data);
        internal static void Warning(string data) => _logSource.LogWarning(data);
    }
}