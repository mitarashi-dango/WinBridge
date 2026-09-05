using System.Windows;
using System.Windows.Controls;

namespace WinBridge.Views;

public partial class PowerView : UserControl
{
    public PowerView() => InitializeComponent();

    private void TimeoutGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 560;
        Grid.SetColumnSpan(AcSettings, compact ? 2 : 1);
        Grid.SetColumn(BatterySettings, compact ? 0 : 1);
        Grid.SetRow(BatterySettings, compact ? 1 : 0);
        Grid.SetColumnSpan(BatterySettings, compact ? 2 : 1);
        BatterySettings.Margin = compact ? new Thickness(0, 20, 0, 0) : new Thickness(20, 0, 0, 0);
    }
}
