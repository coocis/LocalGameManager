using System.Windows;
using System.Windows.Media;

namespace LocalGameManager.Services;

public sealed class ThemeService
{
    public void Apply(string theme)
    {
        var dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        var resources = Application.Current.Resources;
        resources["PageBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#151922" : "#F7F8FC"));
        resources["SurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#202735" : "#FFFFFF"));
        resources["SidebarBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#111520" : "#1D2333"));
        resources["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#EEF1F7" : "#1D2333"));
        resources["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#AEB8CB" : "#69738A"));
        resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#343E51" : "#E2E5EE"));
        resources["InputBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#18202D" : "#FFFFFF"));
        resources["TagBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#2C3B5D" : "#E7ECFF"));
        resources["SelectionBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#354B73" : "#B9DDF0"));
        resources["ScrollTrackBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#171D28" : "#E8EBF2"));
        resources["ScrollThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#53637E" : "#AAB4C9"));
    }
}
