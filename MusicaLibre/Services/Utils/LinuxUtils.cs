namespace MusicaLibre.Services;
using System;

public class LinuxUtils
{
    public enum EnvironmentType
    {
        Unknown,
        KDE,
        GNOME,
        XFCE,
        LXQt,
        Hyprland,
        Sway,
        Niri,
        Other
    }

    public static EnvironmentType AssessEnvironment()
    {
        string? currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.ToLowerInvariant();
        string? session = Environment.GetEnvironmentVariable("DESKTOP_SESSION")?.ToLowerInvariant();
        string? xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.ToLowerInvariant();
        string? waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

        bool Contains(string? value, string keyword)
            => value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;

        // KDE Plasma
        if (Contains(currentDesktop, "kde") || Contains(session, "plasma"))
            return EnvironmentType.KDE;

        // GNOME (including variants like gnome-shell)
        if (Contains(currentDesktop, "gnome") || Contains(session, "gnome"))
            return EnvironmentType.GNOME;

        // XFCE
        if (Contains(currentDesktop, "xfce") || Contains(session, "xfce"))
            return EnvironmentType.XFCE;

        // LXQt
        if (Contains(currentDesktop, "lxqt") || Contains(session, "lxqt"))
            return EnvironmentType.LXQt;

        // Hyprland (Wayland compositor)
        if (Contains(currentDesktop, "hyprland") || Contains(session, "hyprland"))
            return EnvironmentType.Hyprland;

        // Sway (Wayland compositor)
        if (Contains(currentDesktop, "sway") || Contains(session, "sway"))
            return EnvironmentType.Sway;

        // Niri (Wayland compositor)
        if (Contains(currentDesktop, "niri") || Contains(session, "niri"))
            return EnvironmentType.Niri;

        // If we reach here, try to infer Wayland vs X11
        if (!string.IsNullOrEmpty(waylandDisplay) || xdgSessionType == "wayland")
            return EnvironmentType.Other; // Wayland compositor not recognized

        return EnvironmentType.Unknown;
    }

    public static bool IsWayland()
        => string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

}