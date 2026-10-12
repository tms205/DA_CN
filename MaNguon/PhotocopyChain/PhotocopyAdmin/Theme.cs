using MudBlazor;

namespace PhotocopyAdmin;

/// <summary>Màu và font theo Figma (GiaoDien/*.dc.html): accent #22437F, nền #FBF9F5, Be Vietnam Pro.</summary>
public static class Theme
{
    public static readonly MudTheme PhotocopyChain = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0F4C81",
            PrimaryDarken = "#0C3C66",
            PrimaryLighten = "#EDF4FA",
            Secondary = "#334155",
            Success = "#16794A",
            Error = "#B91C1C",
            Warning = "#D97706",
            Background = "#F6F8FA",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#18212F",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#18212F",
            TextPrimary = "#18212F",
            TextSecondary = "#64748B",
            LinesDefault = "#E2E8F0",
            Divider = "#E2E8F0",
            TableLines = "#E2E8F0"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Be Vietnam Pro", "Segoe UI", "system-ui", "sans-serif" }
            }
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" }
    };
}
