using Avalonia;
using Avalonia.Media;

namespace ZenIT.Mac.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(CreateFontManagerOptions())
            .LogToTrace();
    }

    /// <summary>
    /// Inter (the default UI font) has no Arabic glyphs, so Arabic codepoints fall back to the
    /// bundled Noto Sans Arabic family. Without an explicit fallback, bold and semibold Arabic
    /// text renders as missing-glyph boxes on macOS.
    /// </summary>
    public static FontManagerOptions CreateFontManagerOptions()
    {
        return new FontManagerOptions
        {
            DefaultFamilyName = "fonts:Inter#Inter",
            FontFallbacks =
            [
                new FontFallback
                {
                    FontFamily = new FontFamily("avares://ZenIT/Assets/Fonts#Noto Sans Arabic"),
                    UnicodeRange = UnicodeRange.Parse("U+0600-06FF, U+0750-077F, U+08A0-08FF, U+FB50-FDFF, U+FE70-FEFF")
                }
            ]
        };
    }
}
