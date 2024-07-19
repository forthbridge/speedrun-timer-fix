using BepInEx;
using BepInEx.Logging;
using System.Security.Permissions;
using System.Security;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]
#pragma warning restore CS0618 // Type or member is obsolete

namespace SpeedrunTimerFix;

[BepInPlugin(MOD_ID, MOD_ID, "3.0.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string MOD_ID = "speedruntimerfix";
    
    // These are assigned in OnModsInit from modinfo.json, so info doesn't need to be updated in multiple places
    public static string MOD_NAME = "";
    public static string VERSION = "";
    public static string AUTHORS = "";

    public new static ManualLogSource Logger { get; private set; } = null!;

    public void OnEnable()
    {
        Logger = base.Logger;
        Hooks.ApplyInit();
    }
}
