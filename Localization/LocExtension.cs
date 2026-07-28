using System.Windows.Markup;

namespace WinBridge.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string text) => Text = text;

    [ConstructorArgument("text")]
    public string Text { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Text);
}
