namespace Gml.Launcher.Core.Services;

public static class StorageConstants
{
    public const string User = "UserInfo";
    public const string Settings = "Settings";
    public const string LastSelectedProfileName = "LastSelectedProfile";
    public const string InstallationDirectory = "InstallationDirectory";

    /// <summary>Followed by the profile name: the route picked for it (none means automatic).</summary>
    public const string SelectedServerRoutePrefix = "SelectedServerRoute.";
}
