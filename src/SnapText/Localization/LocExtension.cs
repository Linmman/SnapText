using System.Windows.Markup;

namespace SnapText.Localization;

/// <summary>
/// XAML 里取本地化文案：Text="{loc:Loc Key=Settings_Title}"（也支持位置参数写法 {loc:Loc Settings_Title}）。
/// 取值发生在窗口构造时；本应用所有窗口都按需新建，所以切换语言后新开/新弹出的窗口自动使用新语言。
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
