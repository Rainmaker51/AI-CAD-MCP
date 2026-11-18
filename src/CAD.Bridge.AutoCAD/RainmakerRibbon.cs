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
    readonly Dictionary<string, RibbonTab> managedTabs = new();
    int? revision;
    DateTime nextRead;

    internal RainmakerRibbon(Plugin plugin)
    {
        this.plugin = plugin;
        catalog = new CadToolCatalog(plugin.Config.ContentRoot);
    }

    internal void Invalidate() => nextRead = DateTime.MinValue;
    internal void RenderForTest(RibbonControl control,CadToolIndex index){owner=control;Populate(index);}

    internal void Update()
    {
        if(DateTime.UtcNow<nextRead)return;nextRead=DateTime.UtcNow.AddSeconds(4);
        try
        {
            var ribbon=ComponentManager.Ribbon;if(ribbon==null)return;
            if(owner!=ribbon){Remove();owner=ribbon;owner.LayoutUpdated+=CenterVerticalPanels;}
            var index=catalog.Read();
            if(CanRun())plugin.Tools?.Synchronize(index);
            if(revision==index.Revision&&managedTabs.Count>0&&managedTabs.Values.All(ribbon.Tabs.Contains))return;
            Populate(index);revision=index.Revision;
        }
        catch(System.Exception error){System.Diagnostics.Trace.TraceWarning("CAD Manager Ribbon: {0}",error.Message);}
    }

    internal static string NativeTabId(string id)=>id==CadToolCatalog.DefaultTabId?TabId:"AICAD_TOOLS_TAB_"+id;
    void Populate(CadToolIndex index)
    {
        if(owner==null)return;
        string? active=owner.ActiveTab?.Id;
        var definitions=index.Tabs.Where(t=>t.Id==CadToolCatalog.DefaultTabId||t.Enabled).OrderBy(t=>t.Order).ThenBy(t=>t.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        foreach(var previous in managedTabs.Values)owner.Tabs.Remove(previous);
        foreach(string removed in managedTabs.Keys.Where(id=>definitions.All(t=>t.Id!=id)).ToArray())managedTabs.Remove(removed);
        string Target(CadToolEntry tool,CadRibbonPin pin)
        {
            string id=pin.TabId!=""?pin.TabId:tool.TabId;
            return index.Tabs.Any(t=>t.Id==id)?id:CadToolCatalog.DefaultTabId;
        }
        foreach(var definition in definitions)
        {
            if(!managedTabs.TryGetValue(definition.Id,out var tab)){tab=new RibbonTab{Id=NativeTabId(definition.Id)};managedTabs.Add(definition.Id,tab);}
            tab.Title=definition.Name;tab.Panels.Clear();var panels=new Dictionary<string,RibbonPanelSource>(StringComparer.OrdinalIgnoreCase);
            void Add(string group,RibbonButton button)
            {
                if(!panels.TryGetValue(group,out var panel)){panel=new RibbonPanelSource{Title=group};panels.Add(group,panel);tab.Panels.Add(new RibbonPanel{Source=panel});}panel.Items.Add(button);
            }
            var pins=index.Items.Where(t=>t.Kind=="dotnet"&&t.Enabled&&!t.DeletedUtc.HasValue)
                .SelectMany(tool=>tool.RibbonPins.Where(pin=>tool.Commands.Contains(pin.Command,StringComparer.OrdinalIgnoreCase)&&Target(tool,pin)==definition.Id)
                .Select(pin=>new{Tool=tool,Pin=pin,Group=CadToolCatalog.RibbonGroup(tool,pin)}))
                .OrderBy(p=>p.Group,StringComparer.CurrentCultureIgnoreCase).ThenBy(p=>p.Pin.Order).ThenBy(p=>p.Pin.Label,StringComparer.CurrentCultureIgnoreCase);
            foreach(var pin in pins)Add(pin.Group,Button(First(pin.Pin.Label,pin.Pin.Command),First(pin.Pin.Description,pin.Tool.Notes,pin.Pin.Command),pin.Pin.Icon,
                ()=>(plugin.Tools??=new CadToolPalette(plugin)).QueueRibbon(pin.Tool.Id,pin.Pin.Command)));
            Add(ManagerGroup,Button("CAD Tool Manager","Quản lý add-in, tab ribbon và nút lệnh.","Công cụ",()=>Send("AICADTOOLS")));
            owner.Tabs.Add(tab);if(tab.Id==active)tab.IsActive=true;
        }
    }
    static string First(params string[] values) => values.First(value => !string.IsNullOrWhiteSpace(value));

    void CenterVerticalPanels(object? sender, EventArgs e)
    {
        var tab=owner?.ActiveTab;
        if (owner == null || tab == null || !managedTabs.Values.Contains(tab) || owner.RibbonOrientation != Orientation.Vertical)
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
            foreach(var tab in managedTabs.Values)owner.Tabs.Remove(tab);
        }
        managedTabs.Clear();
        revision = null;
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
