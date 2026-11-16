using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AICad.Contracts;
using Autodesk.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AICad.Bridge;

internal sealed class RainmakerRibbon
{
    internal const string TabId = "AICAD_RAINMAKER";
    internal static readonly string[] Icons = { "Công cụ", "Trục", "Tường", "Cửa", "Thống kê" };
    const string ManagerGroup = "Quản lý";
    readonly Plugin plugin;
    readonly CadToolCatalog catalog;
    RibbonControl? owner;
    RibbonTab? tab;
    int? revision;
    DateTime nextRead;

    internal RainmakerRibbon(Plugin plugin)
    {
        this.plugin = plugin;
        catalog = new CadToolCatalog(plugin.Config.ContentRoot);
    }

    internal void Invalidate() => nextRead = DateTime.MinValue;

    internal void Update()
    {
        if (DateTime.UtcNow < nextRead) return;
        nextRead = DateTime.UtcNow.AddSeconds(4);
        try
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            if (tab == null || owner != ribbon || !ribbon.Tabs.Contains(tab))
            {
                Remove();
                owner = ribbon;
                tab = new RibbonTab { Id = TabId, Title = "Rainmaker" };
                Populate(new CadToolIndex());
                ribbon.Tabs.Add(tab);
                ribbon.LayoutUpdated += CenterVerticalPanels;
                revision = null;
            }
            var index = catalog.Read();
            if (revision == index.Revision) return;
            Populate(index);
            revision = index.Revision;
        }
        catch (System.Exception e)
        {
            System.Diagnostics.Trace.TraceWarning("Rainmaker Ribbon: {0}", e.Message);
        }
    }

    void Populate(CadToolIndex index)
    {
        var groups = new Dictionary<string, RibbonPanelSource>(StringComparer.OrdinalIgnoreCase);
        var panels = new List<RibbonPanel>();
        void Add(string group, RibbonButton button)
        {
            if (!groups.TryGetValue(group, out var panel))
            {
                panel = new RibbonPanelSource { Title = group };
                groups.Add(group, panel);
                panels.Add(new RibbonPanel { Source = panel });
            }
            panel.Items.Add(button);
        }

        var pins = index.Items.Where(tool => tool.Enabled && !tool.DeletedUtc.HasValue)
            .Where(tool => tool.Kind == "dotnet")
            .SelectMany(tool => tool.RibbonPins.Where(pin => tool.Commands.Contains(pin.Command, StringComparer.OrdinalIgnoreCase))
                .Select(pin => new { Tool = tool, Pin = pin, Group = First(pin.Group, tool.Group, "Công cụ") }))
            .OrderBy(item => item.Group, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Pin.Order)
            .ThenBy(item => item.Pin.Label, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Pin.Command);
        foreach (var item in pins)
        {
            Add(item.Group, Button(First(item.Pin.Label, item.Pin.Command),
                First(item.Pin.Description, item.Tool.Notes, item.Pin.Command), item.Pin.Icon,
                () => (plugin.Tools ??= new CadToolPalette(plugin)).QueueRibbon(item.Tool.Id, item.Pin.Command)));
        }
        Add(ManagerGroup, Button("CAD Tool Manager", "Thêm, cập nhật và ghim lệnh .NET.", "Công cụ",
            () => Send("AICADTOOLS")));
        tab!.Panels.Clear();
        foreach (var panel in panels) tab.Panels.Add(panel);
    }

    static string First(params string[] values) => values.First(value => !string.IsNullOrWhiteSpace(value));

    void CenterVerticalPanels(object? sender, EventArgs e)
    {
        if (owner == null || tab == null || owner.ActiveTab != tab || owner.RibbonOrientation != Orientation.Vertical)
            return;

        // AutoCAD 2021 docks this part to the right inside the vertical panel template.
        var verticalPanels = FindVisual(owner, element => element.Name == "mVerticalItemsPanel" && element.DataContext == tab);
        if (verticalPanels == null) return;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(verticalPanels); i++)
        {
            var panel = VisualTreeHelper.GetChild(verticalPanels, i);
            var view = FindVisual(panel, element => element is ItemsControl && element.Name == "mRibbonPanelView");
            if (view != null && view.HorizontalAlignment != HorizontalAlignment.Center)
                view.SetCurrentValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        }
    }

    static FrameworkElement? FindVisual(DependencyObject root, Predicate<FrameworkElement> matches)
    {
        if (root is FrameworkElement element && matches(element)) return element;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var match = FindVisual(VisualTreeHelper.GetChild(root, i), matches);
            if (match != null) return match;
        }
        return null;
    }

    static bool CanRun() => AcApp.DocumentManager.MdiActiveDocument != null &&
        Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) == 0;

    static void Send(string command) =>
        AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(command + "\n", true, false, false);

    static RibbonButton Button(string label, string tip, string icon, Action action) => new RibbonButton
    {
        Text = label,
        ShowText = true,
        ShowImage = true,
        Size = RibbonItemSize.Large,
        Orientation = System.Windows.Controls.Orientation.Vertical,
        Image = Icon(icon),
        LargeImage = Icon(icon),
        ToolTip = new RibbonToolTip { Title = label, Content = tip },
        CommandHandler = new ButtonCommand(action)
    };

    static ImageSource Icon(string key)
    {
        string drawing = key switch
        {
            "Trục" => "M 4,15 L 26,15 M 15,4 L 15,26 M 21,15 A 6,6 0 1 1 9,15 A 6,6 0 1 1 21,15",
            "Tường" => "M 4,23 L 4,7 L 26,7 L 26,23 Z M 4,15 L 26,15 M 11,7 L 11,15 M 19,15 L 19,23",
            "Cửa" => "M 5,24 L 25,24 M 6,24 L 6,5 A 19,19 0 0 1 25,24",
            "Thống kê" => "M 4,6 L 26,6 L 26,24 L 4,24 Z M 4,12 L 26,12 M 4,18 L 26,18 M 12,6 L 12,24",
            _ => "M 5,9 L 25,9 L 25,24 L 5,24 Z M 10,9 L 10,5 L 20,5 L 20,9 M 5,16 L 25,16 M 13,16 L 17,16"
        };
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(104, 155, 208)), 1.6);
        var image = new DrawingImage(new GeometryDrawing(null, pen, System.Windows.Media.Geometry.Parse(drawing)));
        image.Freeze();
        return image;
    }

    internal void Remove()
    {
        if (owner != null)
        {
            owner.LayoutUpdated -= CenterVerticalPanels;
            if (tab != null) owner.Tabs.Remove(tab);
        }
        tab = null;
        owner = null;
    }

    sealed class ButtonCommand : ICommand
    {
        readonly Action action;
        internal ButtonCommand(Action action) => this.action = action;
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object parameter) => CanRun();
        public void Execute(object parameter)
        {
            if (CanRun()) action();
        }
    }
}
