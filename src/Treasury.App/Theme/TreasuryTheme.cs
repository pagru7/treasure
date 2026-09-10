using MudBlazor;

namespace Treasury.App.Theme;

public static class TreasuryTheme
{
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1C5B8C",
            Secondary = "#52B69A",
            Tertiary = "#F4B942",
            Background = "#F5F7FB",
            Surface = "#FFFFFF",
            AppbarBackground = "#0F172A",
            AppbarText = "#F8FAFC",
            TextPrimary = "#122033",
            TextSecondary = "#4B5563",
            DrawerBackground = "#111827",
            DrawerText = "#F9FAFB",
            Error = "#E11D48",
            Success = "#10B981",
            Warning = "#F59E0B",
            Info = "#3B82F6"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px"
        }
    };
}