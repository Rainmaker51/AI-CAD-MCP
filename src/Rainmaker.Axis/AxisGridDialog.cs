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
    internal bool Single;
    internal double[] XBays = Array.Empty<double>();
    internal double[] YBays = Array.Empty<double>();
    internal string[] XLabels = Array.Empty<string>();
    internal string[] YLabels = Array.Empty<string>();
    internal string SingleLabel = "1";
    internal int Scale;
}

internal sealed class AxisGridDialog : Window
{
    readonly TextBox xBays = new();
    readonly TextBox yBays = new();
    readonly TextBox xStart = new();
    readonly TextBox yStart = new();
    readonly TextBox singleLabel = new();
    readonly TextBox scale = new();
    readonly TabControl modes = new();
    readonly TextBlock summary = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock dimensionNote;
    readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(253, 164, 175)) };
    readonly Button create = new() { Content = "Chọn điểm đặt lưới", IsDefault = true, MinWidth = 180 };
    static string[] saved = { "4000 4000 4000", "4000 4000", "1", "A", "1", "" };
    internal AxisGridParameters? Parameters { get; private set; }

    internal AxisGridDialog(double currentScale)
    {
        Title = "Rainmaker · Tạo trục";
        Width = 620;
        Height = 760;
        MinWidth = 520;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        dimensionNote = Muted("Dim từng nhịp và dim tổng ở bốn phía, trên layer QA_KT_Dim.", new Thickness(0, 8, 0, 0));
        UseLayoutRounding = true;
        var assembly = typeof(Plugin).Assembly;
        using (var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(".CadToolTheme.xaml"))))
            Resources = (ResourceDictionary)XamlReader.Load(stream!);
        SetResourceReference(BackgroundProperty, "ToolBackground");
        SetResourceReference(ForegroundProperty, "ToolText");

        var root = new DockPanel { Margin = new Thickness(24), LastChildFill = true };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(new TextBlock { Text = "Tạo lưới trục", FontSize = 24, FontWeight = FontWeights.SemiBold });
        header.Children.Add(Muted("Đơn vị mm · Lưới theo WCS XY", new Thickness(0, 6, 0, 0)));
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);
        footer.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(new Button { Content = "Hủy", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 10, 0) });
        create.SetResourceReference(StyleProperty, "ToolPrimaryButton");
        create.Click += (_, _) => Accept();
        actions.Children.Add(create);
        footer.Children.Add(actions);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var body = new StackPanel();
        var gridForm = new StackPanel();
        gridForm.Children.Add(Field("Khoảng cách trục theo X (mm)", xBays, "Ví dụ 4000 5000 3500 hoặc 3*4000"));
        gridForm.Children.Add(Field("Khoảng cách trục theo Y (mm)", yBays, "Mỗi khoảng cách tạo thêm một nhịp giữa hai trục."));
        var labels = new Grid();
        labels.ColumnDefinitions.Add(new ColumnDefinition());
        labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        labels.ColumnDefinitions.Add(new ColumnDefinition());
        labels.Children.Add(Field("Trục đứng bắt đầu từ", xStart, "Ví dụ 1, 5 hoặc A"));
        var yField = Field("Trục ngang bắt đầu từ", yStart, "Ví dụ A, D hoặc 1");
        Grid.SetColumn(yField, 2);
        labels.Children.Add(yField);
        gridForm.Children.Add(labels);
        var singleForm = new StackPanel();
        singleForm.Children.Add(Field("Ký hiệu trục", singleLabel, "Từ 1 đến 8 ký tự. Chọn hai đầu trục sau khi đóng bảng."));
        modes.Items.Add(new TabItem { Header = "Lưới trục", Content = gridForm });
        modes.Items.Add(new TabItem { Header = "Một trục", Content = singleForm });
        modes.SelectionChanged += (_, _) => ValidateForm();
        body.Children.Add(modes);
        body.Children.Add(Field("Mẫu số tỷ lệ", scale, "Ví dụ 60 tương ứng tỷ lệ 1/60. Đầu trục và chữ dim tự điều chỉnh theo tỷ lệ."));
        var info = new StackPanel();
        info.Children.Add(summary);
        info.Children.Add(dimensionNote);
        var summaryBox = new Border { Padding = new Thickness(14), CornerRadius = new CornerRadius(6), Child = info };
        summaryBox.SetResourceReference(BackgroundProperty, "ToolSurface");
        body.Children.Add(summaryBox);
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = root;

        xBays.Text = saved[0]; yBays.Text = saved[1]; xStart.Text = saved[2]; yStart.Text = saved[3]; singleLabel.Text = saved[4];
        int defaultScale = currentScale >= 2 && currentScale <= 1000 ? (int)Math.Round(currentScale) : 60;
        scale.Text = string.IsNullOrEmpty(saved[5]) ? defaultScale.ToString(CultureInfo.InvariantCulture) : saved[5];
        foreach (var field in new[] { xBays, yBays, xStart, yStart, singleLabel, scale })
            field.TextChanged += (_, _) => ValidateForm();
        Loaded += (_, _) => { xBays.Focus(); xBays.SelectAll(); };
        ValidateForm();
    }

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
        try { result.XLabels = AxisGridLayout.Labels(xStart.Text, result.XBays.Length + 1); }
        catch (ArgumentException e) { throw new ArgumentException("Ký hiệu trục đứng — " + e.Message); }
        try { result.YLabels = AxisGridLayout.Labels(yStart.Text, result.YBays.Length + 1); }
        catch (ArgumentException e) { throw new ArgumentException("Ký hiệu trục ngang — " + e.Message); }
        return result;
    }

    void ValidateForm()
    {
        try
        {
            var value = Read();
            error.Text = "";
            create.IsEnabled = true;
            create.Content = value.Single ? "Chọn hai đầu trục" : "Chọn điểm đặt lưới";
            dimensionNote.Visibility = value.Single ? Visibility.Collapsed : Visibility.Visible;
            summary.Text = value.Single ? $"Trục {value.SingleLabel} · Tỷ lệ 1/{value.Scale}" :
                $"{value.XLabels.Length} trục đứng ({value.XLabels.First()}–{value.XLabels.Last()}) · {value.YLabels.Length} trục ngang ({value.YLabels.First()}–{value.YLabels.Last()})\n" +
                $"Kích thước lưới {value.XBays.Sum():N0} × {value.YBays.Sum():N0} mm · Tỷ lệ 1/{value.Scale}";
        }
        catch (ArgumentException e)
        {
            error.Text = e.Message;
            summary.Text = "Điền thông số hợp lệ để xem kích thước lưới.";
            create.IsEnabled = false;
        }
    }

    void Accept()
    {
        ValidateForm();
        if (!create.IsEnabled) return;
        Parameters = Read();
        saved = new[] { xBays.Text, yBays.Text, xStart.Text, yStart.Text, singleLabel.Text, scale.Text };
        DialogResult = true;
    }
}
