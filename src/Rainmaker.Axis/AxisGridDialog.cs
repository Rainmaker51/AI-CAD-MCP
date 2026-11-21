using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using AICad.Bridge;

namespace Rainmaker.Axis;

internal sealed class AxisGridParameters
{
    public bool Single;
    public double[] XBays = Array.Empty<double>();
    public double[] YBays = Array.Empty<double>();
    public string[] XLabels = Array.Empty<string>();
    public string[] YLabels = Array.Empty<string>();
    public string SingleLabel = "1";
    public int Scale;
    internal AxisResolvedLibrary? Library;
    public bool Columns, Walls, WallHatch=true;
    public string ColumnShape="Rectangle";
    public double ColumnB=300,ColumnH=300,ColumnRotation,ColumnOffsetX,ColumnOffsetY;
    public string ColumnAnchor="Center";
    public string[] ColumnNodes=Array.Empty<string>(),WallSegments=Array.Empty<string>();
    public double WallThickness=220,WallOffset;
    public string WallAlignment="Center";
    public bool ShowColumns=true,ShowWalls=true,ShowHatch=true;
    public bool ColumnHatch,DirectedWalls;
    public bool CleanWallJunctions;
    public double ColumnWallInset=110;
    public bool ColumnFaceAnchors,FixedDimExtensions;
    public double DimExtensionLength=300;
    public string GridName="";
    public AxisEnvelope? Envelope;
    public System.Collections.Generic.Dictionary<string,ColumnOptions> ColumnOverrides=new();
    public System.Collections.Generic.Dictionary<string,WallOptions> WallOverrides=new();
}

internal sealed partial class AxisGridDialog : Window
{
    readonly TextBox xBays = new();
    readonly TextBox yBays = new();
    readonly TextBox xStart = new();
    readonly TextBox yStart = new();
    readonly TextBox singleLabel = new();
    readonly TextBox scale = new();
    readonly TabControl modes = new();
    readonly TextBlock summary = new() { TextWrapping = TextWrapping.Wrap };
    TextBlock dimensionNote=null!;
    readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(253, 164, 175)) };
    readonly Button create = new() { Content = "Chọn điểm đặt lưới", IsDefault = true, MinWidth = 180 };
    static string[] saved = { "4000 4000 4000", "4000 4000", "1", "A", "1", "" };
    internal AxisGridParameters? Parameters { get; private set; }

    internal AxisGridDialog(double currentScale,GridDefinition? editing=null) { Editing=editing;Build(currentScale); }

    TextBlock Muted(string text, Thickness margin)
    {
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = margin, FontSize = 12 };
        label.SetResourceReference(ForegroundProperty, "ToolMuted");
        return label;
    }

    StackPanel Field(string title, TextBox input, string hint)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var label = new Label { Content = title, Target = input, Padding = new Thickness(0, 0, 0, 6) };
        label.SetResourceReference(ForegroundProperty, "ToolText");
        AutomationProperties.SetName(input, title);
        input.MinHeight = 38;
        panel.Children.Add(label);
        panel.Children.Add(input);
        panel.Children.Add(Muted(hint, new Thickness(0, 5, 0, 0)));
        return panel;
    }

    AxisGridParameters Read()
    {
        if (!int.TryParse(scale.Text.Trim(), out int denominator) || denominator < 1 || denominator > 1000)
            throw new ArgumentException("Mẫu số tỷ lệ phải là số nguyên từ 1 đến 1000.");
        var result = new AxisGridParameters { Single = modes.SelectedIndex == 1, Scale = denominator, SingleLabel = singleLabel.Text.Trim() };
        ReadConstruction(result);
        if(Editing!=null&&result.Single)throw new ArgumentException("Bấm Tạo mới trước khi chuyển sang Một trục.");
        if (result.Single)
        {
            if (result.SingleLabel.Length < 1 || result.SingleLabel.Length > 8 || result.SingleLabel.Any(char.IsControl))
                throw new ArgumentException("Ký hiệu trục cần từ 1 đến 8 ký tự.");
            return result;
        }
        try { result.XBays = AxisGridLayout.ParseBays(xBays.Text); }
        catch (ArgumentException e) { throw new ArgumentException("Nhịp X — " + e.Message); }
        try { result.YBays = AxisGridLayout.ParseBays(yBays.Text); }
        catch (ArgumentException e) { throw new ArgumentException("Nhịp Y — " + e.Message); }
        result.Envelope=ReadEnvelope();
        string[] Labels(string text,int count){var tokens=text.Split(new[]{' ','\t',',',';'},StringSplitOptions.RemoveEmptyEntries);if(tokens.Length<=1)return AxisGridLayout.Labels(text,count);if(tokens.Length!=count||tokens.Any(t=>t.Length>8)||tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=count)throw new ArgumentException("Cần đúng số ký hiệu, không trùng, tối đa 8 ký tự.");return tokens;}
        try { result.XLabels = Labels(xStart.Text, result.XBays.Length + 1); }
        catch (ArgumentException e) { throw new ArgumentException("Ký hiệu trục đứng — " + e.Message); }
        try { result.YLabels = Labels(yStart.Text, result.YBays.Length + 1); }
        catch (ArgumentException e) { throw new ArgumentException("Ký hiệu trục ngang — " + e.Message); }
        return result;
    }

    void ValidateForm() => UpdateState();

    void Accept()
    {
        ValidateForm();
        if (!create.IsEnabled) return;
        Parameters = Read();
        Parameters.Library = resolved;
        saved = new[] { xBays.Text, yBays.Text, xStart.Text, yStart.Text, singleLabel.Text, scale.Text };
        DialogResult = true;
    }
}
