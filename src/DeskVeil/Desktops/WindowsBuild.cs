namespace DeskVeil.Desktops;

/// <summary>The running Windows build, checked against what VirtualDesktopAccessor supports.</summary>
/// <param name="Build">CurrentBuildNumber, for example 26100.</param>
/// <param name="Revision">UBR (update build revision), for example 2605.</param>
/// <param name="DisplayVersion">For example "24H2"; null when unknown.</param>
public readonly record struct WindowsBuild(int Build, int Revision, string? DisplayVersion)
{
    /// <summary>Windows 11 24H2 with the December 2024 cumulative update, per the VirtualDesktopAccessor README.</summary>
    public const int MinimumBuild = 26100;

    public const int MinimumRevision = 2605;

    public const string SupportedVersionsUrl =
        "https://github.com/Tommy-db-brothers/privacy-desktops-windows#ondersteunde-windows-versies";

    public bool IsSupported => Build > MinimumBuild || (Build == MinimumBuild && Revision >= MinimumRevision);

    public override string ToString() =>
        DisplayVersion is null ? $"build {Build}.{Revision}" : $"{DisplayVersion} (build {Build}.{Revision})";
}
