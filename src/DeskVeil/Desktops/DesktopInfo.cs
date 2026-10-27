namespace DeskVeil.Desktops;

/// <summary>A virtual desktop as seen at one moment. <see cref="Id"/> is the only stable identity;
/// <see cref="Index"/> (0-based) and <see cref="Name"/> change when desktops are reordered or renamed.</summary>
public sealed record DesktopInfo(Guid Id, int Index, string Name);
