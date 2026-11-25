using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AICad.Contracts;
using AICad.LibraryScanner;
using Newtonsoft.Json.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

internal sealed partial class QaBrowserWindow : Window
{
    readonly CancellationTokenSource lifetime = new();
    QaClient? client;
    readonly TreeView tree = new();
    readonly ListBox list = new() { SelectionMode = SelectionMode.Extended };
    readonly StackPanel detail = new();
    readonly TextBox search = new() { MinWidth = 150 };
    readonly ComboBox filter = new() { ItemsSource = new[] { "Có trong CAD", "Mới / đã thay đổi", "Tất cả hiện trạng", "Thùng rác", "Chỉ còn định nghĩa", "Đã mất trong CAD" }, SelectedIndex = 0, MinWidth = 175 };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap }, source = new() { TextWrapping = TextWrapping.Wrap };
    readonly WrapPanel toolbar = new();
    readonly Grid board = new();
    readonly Dictionary<string, BitmapSource> images = new();
    JObject state = new();
    JObject? capture;
    List<LibrarySyncItem> hints = new();
    string kind = "blocks", group = "*";
    bool busy, loading;
    readonly bool persistUi;
    int imageEpoch;
    string Source => state.Value<string>("source") ?? "";
    IEnumerable<JObject> Rows => ResourceRows((state["items"] as JArray ?? new()).OfType<JObject>(),kind);
    // Keep legacy profiles on disk, but expose one native resource in the QA browser.
    internal static IEnumerable<JObject> ResourceRows(IEnumerable<JObject> rows,string kind)
    {
        if(kind!="blocks")return rows;
        return rows.GroupBy(r=>r.Value<string>("name")??r.Value<string>("resourceId")??"",StringComparer.OrdinalIgnoreCase).Select(g=>
        {
            var source=g.OrderByDescending(r=>r.Value<string>("resourceId")==r.Value<string>("name")).ThenBy(r=>r.Value<string>("resourceId"),StringComparer.Ordinal).First();
            var row=(JObject)source.DeepClone();
            row["displayName"]=row["management"]!.Value<string>("Name") is string alias&&alias!=""?alias:row.Value<string>("name");
            return row;
        });
    }
    string UiFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rainmaker", "Library", "ui.json");
    internal QaBrowserWindow(bool test=false)
    {
        persistUi=!test;
        if(!test)kind=Commands.Kind;
        Title = "Rainmaker · Thư viện QA 1.11.1"; Width = 1440; Height = 880; MinWidth = 960; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Resources = Theme.Load(); SetResourceReference(BackgroundProperty, "ToolBackground"); SetResourceReference(ForegroundProperty, "ToolText");
        tree.SetResourceReference(BackgroundProperty,"ToolSurface");tree.SetResourceReference(ForegroundProperty,"ToolText");tree.SetResourceReference(BorderBrushProperty,"ToolBorder");
        tree.Resources[SystemColors.HighlightBrushKey]=FindResource("ToolSelected");tree.Resources[SystemColors.InactiveSelectionHighlightBrushKey]=FindResource("ToolSelected");tree.Resources[SystemColors.ControlBrushKey]=FindResource("ToolSelected");
        tree.Resources[SystemColors.HighlightTextBrushKey]=FindResource("ToolText");tree.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey]=FindResource("ToolText");
        var root = new DockPanel { Margin = new Thickness(18) };root.SetResourceReference(BackgroundProperty,"ToolBackground"); Content = root;
        list.SetResourceReference(BackgroundProperty,"ToolSurface");list.SetResourceReference(ForegroundProperty,"ToolText");list.SetResourceReference(BorderBrushProperty,"ToolBorder");
        var heading = new StackPanel(); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        heading.Children.Add(new TextBlock { Text = "Thư viện CAD", FontSize = 24, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "Đọc DWG và quản lý danh mục kho trực tiếp trong AutoCAD", Margin = new Thickness(0, 4, 0, 8) });
        heading.Children.Add(source);
        var tabs = new WrapPanel { Margin = new Thickness(0, 10, 0, 4) }; heading.Children.Add(tabs);
        foreach (string key in LibrarySyncRules.Kinds)
        {
            string k = key;
            tabs.Children.Add(Button(LibraryFrames.KindName(k), async () => { if (kind == k) return; kind = k;Commands.Kind=k; group = "*"; await Load(); }));
        }
        heading.Children.Add(toolbar);
        toolbar.Children.Add(Button("Mở DWG nguồn", () => { QaCadEditor.Open(Source); return Task.CompletedTask; }));
        toolbar.Children.Add(Button("Quét thay đổi / đối chiếu", ScanCadChanges));
        toolbar.Children.Add(Button("Xử lý cập nhật", ShowRecovery));
        toolbar.Children.Add(Button("Thêm nhóm", () => EditGroup(false)));
        toolbar.Children.Add(Button("Đổi tên / chuyển nhóm", () => EditGroup(true)));
        toolbar.Children.Add(Button("Xóa nhóm", RemoveGroup));
        toolbar.Children.Add(Button("Làm mới CAD", RefreshLive));
        heading.Children.Add(BuildLayoutSection());
        var arrange=Button("Áp dụng nhóm / xếp gọn CAD", async () => {
            DemandLive();
            var items=Rows.Where(r=>r["item"] is JObject && r["management"]?["DeletedUtc"]?.Type==JTokenType.Null).Select(r=>{var item=r["item"]!.ToObject<LibrarySyncItem>()!;item.Group=GroupPath(r["management"]!.Value<string>("GroupId")??"");return item;}).ToList();
            var groupPaths=(state["groups"] as JArray??new()).OfType<JObject>().Select(g=>GroupPath(g.Value<string>("Id")!)).ToList();
            await MutateAndPublish("arrange",(db,tx)=>QaCadEditor.Arrange(Source,kind,items,groupPaths));RefreshCadContext();
        });
        arrange.Tag="arrange";layoutActions.Children.Add(arrange);layoutButtons.Add(arrange);
        toolbar.Children.Add(Button("Tạo ảnh block từ CAD", async () => { if(kind!="blocks")throw new InvalidOperationException("Chọn tab Block để tạo ảnh.");await RefreshLive();QaCadSnapshot.Previews(client!.Config,capture!,Rows);images.Clear();await Load();status.Text="Đã tạo ảnh 240 × 180 từ hiện trạng CAD; chỉ lưu bộ nhớ đệm trên máy này."; }));
        var footer = new Border { Padding = new Thickness(0, 10, 0, 0), Child = status }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        board.ColumnDefinitions.Add(new() { Width = new GridLength(220), MinWidth = 160 });
        board.ColumnDefinitions.Add(new() { Width = new GridLength(7) });
        board.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        board.ColumnDefinitions.Add(new() { Width = new GridLength(7) });
        board.ColumnDefinitions.Add(new() { Width = new GridLength(360), MinWidth = 240 });
        root.Children.Add(board);
        PanelAt(tree, 0);
        var middle = new DockPanel { Margin = new Thickness(8, 0, 8, 0) };
        var filters = new Grid { Margin = new Thickness(0, 0, 0, 8) }; filters.ColumnDefinitions.Add(new()); filters.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        filters.RowDefinitions.Add(new(){Height=GridLength.Auto});filters.RowDefinitions.Add(new(){Height=GridLength.Auto});
        filters.Children.Add(new TextBlock{Text="Tìm mã, tên, công dụng hoặc thông số",Margin=new Thickness(0,0,0,5)});
        search.ToolTip = "Tìm mã, tên, công dụng, thông số hoặc layer"; System.Windows.Automation.AutomationProperties.SetName(search, "Tìm thư viện"); Grid.SetRow(search,1);filters.Children.Add(search); Grid.SetColumn(filter, 1);Grid.SetRow(filter,1); filter.Margin = new Thickness(8, 0, 0, 0); filters.Children.Add(filter); DockPanel.SetDock(filters, Dock.Top); middle.Children.Add(filters);
        var selection = new WrapPanel(); DockPanel.SetDock(selection, Dock.Bottom); middle.Children.Add(selection);
        selection.Children.Add(Button("Chuyển nhóm mục chọn", MoveSelection));
        selection.Children.Add(Button("Loại hồ sơ đã mất", RemoveMissingProfiles));
        middle.Children.Add(list); PanelAt(middle, 2);
        PanelAt(new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(12) }, 4);
        foreach (int column in new[] { 1, 3 })
        {
            var split = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
            split.SetResourceReference(BackgroundProperty, "ToolBorder"); Grid.SetColumn(split, column); board.Children.Add(split);
        }
        if(persistUi)LoadUi();
        search.TextChanged += (_, _) => RenderList(); filter.SelectionChanged += (_, _) => RenderList();
        BuildHatchControls(middle);
        tree.SelectedItemChanged += (_, _) => { if (loading) return; group = (tree.SelectedItem as TreeViewItem)?.Tag as string ?? "*"; RenderList();RefreshCadContext(); };
        list.SelectionChanged += (_, _) => ShowDetail();
        if(!test)Loaded += async (_, _) => await Run(async () => { client = new QaClient(); await Load(); if(state["authority"]?.Value<string>("editor")!="QA"){await Api("activate",new());await Load();} });
        Closed += (_, _) => { if(persistUi)SaveUi(); lifetime.Cancel();if(client!=null&&capture!=null)try{QaCadSnapshot.Discard(client.Config,capture);}catch(IOException){} client?.Dispose(); };
    }
    void PanelAt(UIElement child, int column) { Grid.SetColumn(child, column); board.Children.Add(child); }
    Button Button(string title, Func<Task> action)
    {
        var button = new Button { Content = title, Margin = new Thickness(0, 0, 6, 6), MinHeight = 34, Padding = new Thickness(10, 6, 10, 6) };
        button.Click += async (_, _) => await Run(action); return button;
    }
    async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; toolbar.IsEnabled = false; status.Text = "Đang xử lý…";
        try { await action(); }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) status.Text = "Đã hủy thao tác."; }
        catch (Exception error) { status.Text = error.Message; }
        finally { busy = false; toolbar.IsEnabled = true; }
    }
    Task<JObject> Api(string path, JObject? body = null) => client!.Call("api/qa-library/" + path, body, lifetime.Token);
    async Task Load()
    {
        if (client == null) return;
        if(capture==null){await RefreshLive();return;}
        var data=await Api("live/"+capture.Value<string>("id")+"/"+kind);
        if(kind=="blocks")
        {
            string folder=QaCadSnapshot.Folder(client.Config,capture),file=Path.Combine(folder,"previews.json");
            if(!File.Exists(file))QaCadSnapshot.Previews(client.Config,capture,(data["items"] as JArray??new()).OfType<JObject>());
            if(File.Exists(file))
            {
                var previews=JObject.Parse(File.ReadAllText(file));
                foreach(var row in (data["items"] as JArray??new()).OfType<JObject>())
                {
                    string id=QaCadSnapshot.PreviewKey(row),expected=AICad.Contracts.Json.Hash(id)+".png";
                    if(previews[id]?.Value<string>("imageFile")==expected&&File.Exists(Path.Combine(folder,expected)))row["image"]="qa-local:"+expected;
                }
            }
        }
        Bind(data);
    }
    async Task RefreshLive()
    {
        if(client==null)return;
        var published=await Api(kind);
        JObject? next=null;
        try
        {
        var sync=await client.Call("api/library-sync",null,lifetime.Token);
        hints=(sync["Items"]??sync["items"]??new JArray()).ToObject<List<LibrarySyncItem>>()!;
        next=QaCadSnapshot.Capture(client.Config,published.Value<string>("source")!,hints);
        await Api("live/register",new(){["id"]=next["id"]!.DeepClone()});
        var previous=capture;capture=next;
        images.Clear();await Load();
        if(previous!=null)try{QaCadSnapshot.Discard(client.Config,previous);}catch(IOException){}
        }
        catch(Exception error)when(!lifetime.IsCancellationRequested)
        {
            if(next!=null)try{QaCadSnapshot.Discard(client.Config,next);}catch(IOException){}
            CatalogFallback(published);
            throw new InvalidOperationException("Danh mục kho vẫn còn nguyên. Chưa đọc được CAD: "+error.Message+" Nhấn Làm mới CAD để thử lại.",error);
        }
    }
    internal void CatalogFallback(JObject published)
    {
        capture=null;Bind(published);
        source.Text+=" · CHỈ XEM DANH MỤC KHO — chưa đọc được hiện trạng CAD";
    }
    internal void Bind(JObject data)
    {
        state = data; source.Text = Path.GetFileName(Source) + "  ·  " + Source+ (state.Value<bool?>("live")==true?state.Value<string>("readMode")=="OPEN_DOCUMENT"?" · Hiện trạng DWG đang mở":" · DWG đã lưu":" · Danh mục đã lưu");
        loading = true; tree.Items.Clear();
        TreeViewItem TreeRow(string text,string id){var r=new TreeViewItem{Header=text,Tag=id};r.SetResourceReference(ForegroundProperty,"ToolText");return r;}
        tree.Items.Add(TreeRow("Tất cả " + LibraryFrames.KindName(kind),"*"));
        tree.Items.Add(TreeRow("Chưa phân nhóm",""));
        var groups = (state["groups"] as JArray ?? new()).OfType<JObject>().ToArray();
        void AddGroups(ItemsControl parent, string parentId, HashSet<string> visited)
        {
            foreach (var g in groups.Where(g => g.Value<string>("ParentId") == parentId).OrderBy(g => g.Value<string>("Name")))
            {
                string id = g.Value<string>("Id")!; if (!visited.Add(id)) continue;
                var row = TreeRow(g.Value<string>("Name")??"",id);
                parent.Items.Add(row); AddGroups(row, id, visited); if (id == group) row.IsSelected = true;
            }
        }
        AddGroups(tree, "", new()); loading = false; BindHatchControls();RenderList();RefreshCadContext();
        status.Text = $"{LibraryFrames.KindName(kind)} · {Rows.Count()} mục · danh mục kho v{state.Value<string>("version")} · " + (state["authority"]?.Value<string>("editor") == "QA" ? "Chỉnh sửa tại QA" : "Chưa chuyển app sang chỉ xem");
        int pending=Rows.Count(r=>r.Value<string>("liveStatus") is "NEW" or "CHANGED" or "MISSING" or "DEFINITION_ONLY");
        if(pending>0)status.Text+=" · Chưa khớp CAD/kho: "+pending+" mục";
        if(client!=null&&File.Exists(PendingFile))status.Text+=" · Có thao tác chưa hoàn tất — nhấn Xử lý cập nhật";
    }
    bool InGroup(string id)
    {
        if (group == "*") return true;
        var groups = (state["groups"] as JArray ?? new()).OfType<JObject>().ToDictionary(g => g.Value<string>("Id")!);
        var seen = new HashSet<string>();
        while (id != "" && seen.Add(id)) { if (id == group) return true; id = groups.TryGetValue(id, out var g) ? g.Value<string>("ParentId") ?? "" : ""; }
        return group == "" && id == "" && seen.Count == 0;
    }
    void RenderList()
    {
        if (loading) return;
        int epoch = ++imageEpoch; list.ItemsSource=null; list.Items.Clear(); list.GroupStyle.Clear(); detail.Children.Clear();
        bool cards=kind is "blocks" or "hatches";
        list.ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(cards?typeof(WrapPanel):typeof(VirtualizingStackPanel)));
        string query = search.Text.Trim();
        var visible=new List<ListBoxItem>();
        foreach (var row in Rows.Where(r => InGroup(r["management"]!.Value<string>("GroupId") ?? "")))
        {
            var m = row["management"]!; bool deleted = m["DeletedUtc"]?.Type != JTokenType.Null && m["DeletedUtc"] != null;
            if (filter.SelectedIndex == 3 ? !deleted : deleted) continue;
            string live=row.Value<string>("liveStatus")??"PRESENT";
            if(filter.SelectedIndex!=3&&filter.SelectedIndex!=2&&(filter.SelectedIndex==6?live!="OUT_OF_SCOPE":filter.SelectedIndex==4?live!="DEFINITION_ONLY":filter.SelectedIndex==5?live!="MISSING":live is "DEFINITION_ONLY" or "MISSING" or "OUT_OF_SCOPE"))continue;
            if(filter.SelectedIndex==1&&live is not ("NEW" or "CHANGED"))continue;
            if(!MatchesPattern(row))continue;
            if (query != "" && row.ToString().IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            var content = new Grid { Margin = new Thickness(5), MinHeight = cards ? 200 : 44, Width=cards?175:double.NaN };
            content.RowDefinitions.Add(new(){Height=GridLength.Auto});content.RowDefinitions.Add(new(){Height=GridLength.Auto});
            var image = new Image { Width = 165, Height = cards?110:0, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 8) }; content.Children.Add(image);
            var text = new StackPanel(); Grid.SetRow(text, 1); content.Children.Add(text);
            text.Children.Add(new TextBlock { Text = row.Value<string>("displayName"), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = row.Value<string>("code"), Margin = new Thickness(0, 4, 0, 0),TextWrapping=TextWrapping.Wrap });
            if(state.Value<bool?>("live")==true)text.Children.Add(new TextBlock{Text=LiveLabel(row),TextWrapping=TextWrapping.Wrap});
            if(kind=="hatches")text.Children.Add(new TextBlock{Text=HatchSummary(row),TextWrapping=TextWrapping.Wrap});
            text.Children.Add(new TextBlock { Text = m.Value<string>("Purpose"), TextWrapping = TextWrapping.Wrap });
            var item = new ListBoxItem { Content = content, Tag = row, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            item.SetResourceReference(ForegroundProperty,"ToolText");item.SetResourceReference(BackgroundProperty,"ToolSurface");
            System.Windows.Automation.AutomationProperties.SetName(item, row.Value<string>("displayName")); visible.Add(item);
            string path = row.Value<string>("image") ?? ""; if (path != "") _ = LoadImage(image, path, epoch);
        }
        SetHatchView(visible);
    }
    internal static bool UseReviewed(JObject row)=>row.Value<bool?>("useReviewed")??((row["profile"]?["Usage"] as JObject)?.Value<bool?>("Reviewed")??row["management"]?.Value<bool?>("Reviewed")??false);
    static string LiveLabel(JObject row)=>row.Value<string>("liveStatus") switch{"OUT_OF_SCOPE"=>"Ngoài khung quét · danh mục được giữ nguyên","NEW"=>"Mới trong CAD · chưa lưu danh mục","CHANGED"=>"CAD đã thay đổi","MISSING"=>"Không còn trong CAD","DEFINITION_ONLY"=>"Chỉ có định nghĩa · không có mẫu",_=>"Có trong CAD · đã có danh mục"};
    async Task LoadImage(Image target, string path, int epoch)
    {
        try
        {
            if (!images.TryGetValue(path, out var image))
            {
                byte[]? bytes;
                if(path.StartsWith("qa-local:",StringComparison.Ordinal)&&capture!=null&&System.Text.RegularExpressions.Regex.IsMatch(path.Substring(9),"^[a-f0-9]{64}\\.png$"))bytes=File.ReadAllBytes(Path.Combine(QaCadSnapshot.Folder(client!.Config,capture),path.Substring(9)));
                else bytes=await client!.Image(path, lifetime.Token);
                if (bytes == null || epoch != imageEpoch) return;
                using var stream = new MemoryStream(bytes); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 200; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); images[path] = image = bitmap;
            }
            if (epoch == imageEpoch) target.Source = image;
        }
        catch (Exception e) when (e is IOException || e is OperationCanceledException || e is System.Net.Http.HttpRequestException || e is NotSupportedException) { }
    }
    JObject? Selected => (list.SelectedItem as ListBoxItem)?.Tag as JObject;
    List<JObject> SelectedRows => list.SelectedItems.OfType<ListBoxItem>().Select(r => (JObject)r.Tag).ToList();
    void ShowDetail()
    {
        detail.Children.Clear(); var row = Selected; if (row == null) return;
        var m = row["management"]!;
        Label(row.Value<string>("displayName") ?? "", 20); Label(row.Value<string>("code") ?? "");
        Label("Tên trong DWG: " + row.Value<string>("name")); Label("ID ổn định: " + row.Value<string>("id"));
        if(state.Value<bool?>("live")==true)Label(LiveLabel(row));
        Label("Công dụng: " + m.Value<string>("Purpose")); Label("Ghi chú: " + m.Value<string>("Notes"));
        detail.Children.Add(Button("Tên / công dụng / ghi chú", EditMetadata));
        if(kind=="blocks"&&row.Value<string>("liveStatus") is not ("MISSING" or "DEFINITION_ONLY" or "OUT_OF_SCOPE"))detail.Children.Add(Button("Chèn block vào bản vẽ hiện tại",InsertSelected));
        detail.Children.Add(new TextBlock{Text="Thêm/sửa thông số trực tiếp trên CAD. Ctrl+S rồi Quét thay đổi / đối chiếu.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)});
        Label("File nguồn: " + Path.GetFileName(Source)); Label("Đường dẫn: " + Source);
        var original=(JObject)row["profile"]!.DeepClone();original.Remove("Usage");
        var parameters = Parameters(original);
        detail.Children.Add(new Expander { Header = "Thông số tài nguyên", Content = parameters, IsExpanded = false, Margin = new Thickness(0, 14, 0, 0) });
    }
    static StackPanel Parameters(JToken value)
    {
        var panel=new StackPanel();
        foreach(var p in value is JObject obj?obj.Properties().Select(p=>(p.Name,p.Value)):value.Children().Select((v,i)=>("#"+(i+1),v)))
        {
            if(p.Item2 is JObject or JArray)panel.Children.Add(new Expander{Header=p.Item1,Content=Parameters(p.Item2),IsExpanded=false});
            else panel.Children.Add(new TextBlock{Text=p.Item1+": "+p.Item2,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)});
        }
        return panel;
    }
    void Label(string text, double size = 13) => detail.Children.Add(new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
    JObject MetadataRequest(JObject row)
    {
        DemandPublished(row);
        var m = row["management"]!;
        return new() { ["kind"] = kind, ["metadataId"] = row["metadataId"]!.DeepClone(), ["catalogRevision"] = state["revision"]!.DeepClone(), ["revision"] = m["Revision"]!.DeepClone(), ["name"] = row["displayName"]!.DeepClone(), ["purpose"] = m["Purpose"]!.DeepClone(), ["notes"] = m["Notes"]!.DeepClone(), ["groupId"] = m["GroupId"]!.DeepClone(), ["reviewed"] = m["Reviewed"]!.DeepClone(), ["action"] = "update" };
    }
    async Task EditMetadata()
    {
        DemandLive();
        var row = Selected; if (row == null) return;
        var p = MetadataRequest(row); var dialog = new QaForm("Thông tin " + row.Value<string>("code"), p, new[] { "name", "purpose", "notes", "reviewed" });
        if (AcApp.ShowModalWindow(dialog) != true) return; await SyncMetadata(new(){dialog.Value});
    }
    async Task MoveSelection()
    {
        DemandLive();
        var rows = SelectedRows; if (rows.Count == 0) throw new InvalidOperationException("Chọn một hoặc nhiều mục trong danh sách.");
        var dialog = GroupChoice("Chuyển " + rows.Count + " mục sang nhóm", ""); if (AcApp.ShowModalWindow(dialog) != true) return;
        var requests=new List<JObject>();foreach(var row in rows){var p=MetadataRequest(row);p["groupId"]=dialog.Choice;requests.Add(p);}await SyncMetadata(requests);
    }
    async Task TrashSelection()
    {
        if(filter.SelectedIndex!=3){await DeleteSelectedResources();return;}
        DemandLive();
        var rows = SelectedRows; if (rows.Count == 0) return;
        bool restore = filter.SelectedIndex == 3;
        if (MessageBox.Show(this, (restore ? "Khôi phục " : "Đưa vào thùng rác ") + rows.Count + " hồ sơ? Mẫu trong DWG được giữ nguyên.", "Thư viện QA", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        if(rows.Any(r=>r.Value<string>("liveStatus") is "MISSING" or "DEFINITION_ONLY"))throw new InvalidOperationException("Mẫu không còn trong CAD. Khôi phục mẫu từ bản sao DWG trước khi khôi phục danh mục.");
        foreach (var row in rows) { var p = MetadataRequest(row); p["action"] = restore ? "restore" : "delete"; await Api("manage", p); }
        await Load();
    }
    QaChoice GroupChoice(string title, string selected) => new(title, (state["groups"] as JArray ?? new()).OfType<JObject>().Select(g => (g.Value<string>("Id")!, GroupPath(g.Value<string>("Id")!))).Prepend(("", "Chưa phân nhóm")).ToArray(), selected);
    string GroupPath(string id)
    {
        var groups = (state["groups"] as JArray ?? new()).OfType<JObject>().ToDictionary(g => g.Value<string>("Id")!); var parts = new List<string>(); var visited = new HashSet<string>();
        while (id != "" && groups.TryGetValue(id, out var g) && visited.Add(id)) { parts.Insert(0, g.Value<string>("Name")!); id = g.Value<string>("ParentId") ?? ""; }
        return string.Join(" / ", parts);
    }
    async Task EditGroup(bool edit)
    {
        DemandLive();
        var current = (state["groups"] as JArray ?? new()).OfType<JObject>().SingleOrDefault(g => g.Value<string>("Id") == group);
        if (edit && current == null) throw new InvalidOperationException("Chọn một nhóm để chỉnh sửa.");
        string? name = NameDialog.Ask("Nhóm " + LibraryFrames.KindName(kind), "Tên nhóm", current?.Value<string>("Name") ?? ""); if (string.IsNullOrWhiteSpace(name)) return;
        var parent = GroupChoice("Nhóm cha", edit ? current!.Value<string>("ParentId") ?? "" : group == "*" ? "" : group);
        if (AcApp.ShowModalWindow(parent) != true) return;
        await SyncGroup(new() { ["kind"] = kind, ["id"] = edit ? group : "", ["name"] = name, ["parentId"] = parent.Choice });
    }
    async Task RemoveGroup()
    {
        DemandLive();
        if (group is "*" or "") throw new InvalidOperationException("Chọn nhóm cần xóa.");
        var groups=state["groups"]!.ToObject<List<LibraryGroup>>()!;string path=TreePath(groups,group);
        var targets=new[]{new QaCadReview.Pick{Key="",Text="Chưa phân nhóm / khung gốc"}}.Concat(groups.Where(g=>g.Id!=group&&!LibraryFrames.Under(TreePath(groups,g.Id),path)).Select(g=>new QaCadReview.Pick{Key=g.Id,Text=TreePath(groups,g.Id)})).ToArray();
        int profiles=Rows.Count(r=>r["management"]?.Value<string>("GroupId")==group);
        int samples=(capture!["items"] as JArray??new()).OfType<JObject>().Count(i=>i.Value<string>("Kind")==kind&&i.Value<string>("Group")==path);
        var dialog=new Window{Title="Xóa nhóm · "+path,Width=620,Height=410,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Resources=Theme.Load()};dialog.SetResourceReference(BackgroundProperty,"ToolBackground");dialog.SetResourceReference(ForegroundProperty,"ToolText");
        var panel=new StackPanel{Margin=new Thickness(20)};dialog.Content=panel;
        panel.Children.Add(new TextBlock{Text=$"{profiles} hồ sơ trực tiếp · {samples} tài nguyên CAD nhận diện · {groups.Count(g=>g.ParentId==group)} nhóm con trực tiếp.\nXóa nhóm và khung tương ứng; giữ nguyên tài nguyên, chuyển mẫu cùng chú thích sang nhóm đích. Khung đã xóa trên CAD cũng có thể xử lý tại đây.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
        panel.Children.Add(new TextBlock{Text="Nhóm nhận mẫu"});var destination=new ComboBox{ItemsSource=targets,SelectedIndex=0,Margin=new Thickness(0,5,0,12)};panel.Children.Add(destination);
        panel.Children.Add(new TextBlock{Text="Xử lý nhóm con"});var children=new ComboBox{ItemsSource=new[]{"Đưa nhóm con lên cấp cha","Chuyển các nhóm con sang nhóm đích"},SelectedIndex=0,Margin=new Thickness(0,5,0,14)};panel.Children.Add(children);
        var buttons=new WrapPanel();panel.Children.Add(buttons);var confirm=new Button{Content="Xóa nhóm và chuyển mẫu",Padding=new Thickness(10),Margin=new Thickness(0,0,8,0)};buttons.Children.Add(confirm);confirm.Click+=(_,_)=>dialog.DialogResult=true;buttons.Children.Add(new Button{Content="Hủy",IsCancel=true,Padding=new Thickness(10)});
        if(AcApp.ShowModalWindow(dialog)!=true)return;
        await SyncGroup(new() { ["kind"] = kind, ["id"] = group, ["action"] = "remove",["targetId"]=((QaCadReview.Pick)destination.SelectedItem).Key,["childMode"]=children.SelectedIndex==1?"branch":"lift" }); group = "*"; await Load();
    }
    async Task EditCad(bool create)
    {
        try{await EditCadCore(create);}
        catch(OperationCanceledException){throw;}
        catch(Exception error){MessageBox.Show(this,error.Message,"Chưa cập nhật thư viện",MessageBoxButton.OK,MessageBoxImage.Warning);throw;}
    }
    async Task EditCadCore(bool create)
    {
        DemandLive();
        var row = Selected;
        if (create && kind == "blocks") {
            var file=new Microsoft.Win32.OpenFileDialog{Title="Chọn DWG chứa block",Filter="AutoCAD DWG|*.dwg"};if(file.ShowDialog(this)!=true)return;
            string[] names=QaCadEditor.BlockNames(file.FileName);if(names.Length==0)throw new InvalidOperationException("DWG không có định nghĩa block có tên. Tạo block trong CAD trước khi nhập.");
            var choose=new QaChoice("Block cần nhập",names.Select(n=>(n,n)).ToArray(),names[0]);if(AcApp.ShowModalWindow(choose)!=true)return;
            await MutateAndPublish("import-block",(db,tx)=>QaCadEditor.ImportBlock(Source,file.FileName,choose.Choice,group=="*"?"":GroupPath(group),this.hints));return;
        }
        if (!create && row?["item"] is not JObject) throw new InvalidOperationException("Tài nguyên chưa được đối chiếu. Lưu nguồn và đọc kiểm tra trước.");
        var item = !create ? row!["item"]!.ToObject<LibrarySyncItem>()! : new LibrarySyncItem { Id = Guid.NewGuid().ToString("N"), Kind = kind, Values = Defaults(kind), Group = group == "*" ? "" : GroupPath(group) };
        if(!create&&item.Values["qaDisplayName"]==null&&row!["management"]?.Value<string>("Name") is string alias&&alias!=""&&alias!=item.Name)item.Values["qaDisplayName"]=alias;
        var editable = kind switch { "blocks" => Array.Empty<string>(), "layers" => new[] { "color", "rgb", "lineWeight", "lineType", "plottable" }, "texts" => new[] { "font", "bigFont", "height", "widthFactor", "obliqueAngle", "vertical" }, "dims" => new[] { "textStyle", "annotative", "textHeight", "arrowSize", "scale", "linearFactor", "precision", "extensionOffset", "extensionBeyond", "baselineSpacing", "textGap", "rounding", "unitFormat", "angularPrecision", "zeroSuppression" }, _ => new[] { "pattern", "type", "scale", "angle", "layer", "color", "rgb", "spacing", "doublePattern" } };
        var data = new JObject { ["name"] = item.Name }; foreach (var p in item.Values.Properties().Where(p => editable.Contains(p.Name) && p.Value is JValue)) data[p.Name] = p.Value.DeepClone();
        var form = new QaForm((create ? "Thêm " : "Sửa ") + LibraryFrames.KindName(kind) + " trong DWG", data, data.Properties().Select(p => p.Name).ToArray());
        if (AcApp.ShowModalWindow(form) != true) return;
        item.Name = form.Value.Value<string>("name") ?? ""; foreach (var p in form.Value.Properties().Where(p => p.Name != "name")) item.Values[p.Name] = p.Value.DeepClone();
        await MutateAndPublish("edit-resource",(db,tx)=>LibrarySyncNative.EditResource(db,tx,item,this.hints));
    }
    static JObject Defaults(string kind) => kind switch {
        "layers" => JObject.Parse("{color:7,lineWeight:25,lineType:'Continuous',plottable:true}"),
        "texts" => JObject.Parse("{font:'tahoma.ttf',bigFont:'',height:0,widthFactor:1,obliqueAngle:0,vertical:false}"),
        "dims" => JObject.Parse("{textStyle:'Standard',annotative:false,textHeight:2.5,arrowSize:2.5,scale:1,linearFactor:1,precision:0,extensionOffset:1,extensionBeyond:1,baselineSpacing:7,textGap:1,rounding:0,unitFormat:2,angularPrecision:0,zeroSuppression:0}"),
        _ => JObject.Parse("{pattern:'SOLID',type:'PreDefined',scale:1,angle:0,layer:'0',color:256,spacing:1,doublePattern:false}") };
    async Task Inspect()
    {
        await ScanCadChanges();
    }
    static void DemandPublished(JObject row){if(row.Value<bool?>("published")==false)throw new InvalidOperationException("Mục mới trong CAD. Lưu DWG và dùng Lưu danh mục vào kho trước khi chỉnh hồ sơ sử dụng.");}
    void DemandLive(){if(capture==null)throw new InvalidOperationException("Đang xem danh mục kho. Làm mới CAD thành công trước khi chỉnh sửa.");}
    void LoadUi()
    {
        try { if (!File.Exists(UiFile)) return; var saved = JObject.Parse(File.ReadAllText(UiFile)); Width = Math.Max(MinWidth, Math.Min(SystemParameters.WorkArea.Width, saved.Value<double?>("width") ?? Width)); Height = Math.Max(MinHeight, Math.Min(SystemParameters.WorkArea.Height, saved.Value<double?>("height") ?? Height)); foreach (int c in new[] { 0, 4 }) board.ColumnDefinitions[c].Width = new GridLength(Math.Max(c == 0 ? 160 : 240, Math.Min(600, saved.Value<double?>("column" + c) ?? (c == 0 ? 220 : 360)))); } catch (Exception e) when (e is IOException || e is Newtonsoft.Json.JsonException) { }
    }
    void SaveUi()
    {
        try { if(RestoreBounds.IsEmpty)return;Directory.CreateDirectory(Path.GetDirectoryName(UiFile)!); AICad.Contracts.Json.Atomic(UiFile, new { width = RestoreBounds.Width, height = RestoreBounds.Height, column0 = board.ColumnDefinitions[0].ActualWidth, column4 = board.ColumnDefinitions[4].ActualWidth }); } catch (IOException) { }
    }
}

internal sealed class QaForm : Window
{
    internal JObject Value { get; }
    internal QaForm(string title, JObject input, string[] keys,Dictionary<string,string[]>? options=null)
    {
        Value = (JObject)input.DeepClone(); Title = title; Width = 540; Height = 650; MinHeight = 280; WindowStartupLocation = WindowStartupLocation.CenterOwner; Resources = Theme.Load(); SetResourceReference(BackgroundProperty, "ToolBackground"); SetResourceReference(ForegroundProperty, "ToolText");
        var panel = new StackPanel { Margin = new Thickness(20) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var inputs = new Dictionary<string, FrameworkElement>(); var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (string key in keys)
        {
            panel.Children.Add(new TextBlock { Text = Labels.TryGetValue(key, out var label) ? label : key, Margin = new Thickness(0, 8, 0, 4) });
            FrameworkElement field = options!=null&&options.TryGetValue(key,out var choices)?new ComboBox{ItemsSource=choices,SelectedItem=choices.Contains(input[key]?.ToString()??"")?input[key]?.ToString():choices.FirstOrDefault(),MinHeight=34}:input[key]?.Type == JTokenType.Boolean ? new CheckBox { IsChecked = input.Value<bool>(key), MinHeight = 28 } : new TextBox { Text = input[key]?.Type == JTokenType.Null ? "" : input[key]?.ToString() ?? "", MinHeight = 34, AcceptsReturn = key is "notes" or "purpose", TextWrapping = TextWrapping.Wrap };
            System.Windows.Automation.AutomationProperties.SetName(field, key); inputs[key] = field; panel.Children.Add(field);
        }
        panel.Children.Add(error); var buttons = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) }; panel.Children.Add(buttons);
        var ok = new Button { Content = "Lưu", IsDefault = true, MinWidth = 100 }; buttons.Children.Add(ok); var cancel = new Button { Content = "Hủy", IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) }; buttons.Children.Add(cancel);
        ok.Click += (_, _) => {
            try { foreach (var pair in inputs) { string key = pair.Key; if (pair.Value is CheckBox check) Value[key] = check.IsChecked == true; else { string text = pair.Value is ComboBox combo?combo.SelectedItem?.ToString()??"":((TextBox)pair.Value).Text.Trim(); var type = input[key]?.Type; bool integer = new[] { "color", "rgb", "lineWeight", "precision", "unitFormat", "angularPrecision", "zeroSuppression" }.Contains(key); Value[key] = integer && text != "" ? new JValue(long.Parse(text, CultureInfo.InvariantCulture)) : type is JTokenType.Integer or JTokenType.Float ? new JValue(double.Parse(text, CultureInfo.InvariantCulture)) : type == JTokenType.Null && text == "" ? JValue.CreateNull() : new JValue(text); } } DialogResult = true; } catch (Exception e) when (e is FormatException || e is OverflowException) { error.Text = "Thông số số dùng dấu chấm thập phân. Kiểm tra lại các ô."; }
        };
    }
    static readonly Dictionary<string, string> Labels = new() { ["name"] = "Tên", ["purpose"] = "Công dụng", ["notes"] = "Ghi chú", ["reviewed"] = "Đã kiểm tra / cho phép Skill sử dụng", ["color"] = "Màu ACI (256 = ByLayer)", ["lineWeight"] = "Độ dày nét (1/100 mm)", ["lineType"] = "Kiểu nét", ["scale"] = "Tỷ lệ", ["angle"] = "Góc (radian)", ["font"] = "Tên file font", ["height"] = "Chiều cao", ["widthFactor"] = "Hệ số rộng", ["pattern"] = "Tên mẫu hatch", ["layer"] = "Layer", ["textStyle"] = "Style chữ", ["textHeight"] = "Chiều cao chữ dim", ["arrowSize"] = "Kích thước mũi tên" };
}

internal sealed class QaChoice : Window
{
    readonly ComboBox choices;
    internal string Choice => ((ChoiceItem)choices.SelectedItem).Id;
    sealed class ChoiceItem(string id, string name) { public string Id => id; public string Name => name; }
    internal QaChoice(string title, (string id, string name)[] entries, string selected)
    {
        Title = title; Width = 520; Height = 220; WindowStartupLocation = WindowStartupLocation.CenterOwner; Resources = Theme.Load(); SetResourceReference(BackgroundProperty, "ToolBackground"); SetResourceReference(ForegroundProperty, "ToolText");
        var panel = new StackPanel { Margin = new Thickness(20) }; Content = panel; panel.Children.Add(new TextBlock { Text = title });
        var items = entries.Select(e => new ChoiceItem(e.id, e.name)).ToArray(); choices = new ComboBox { ItemsSource = items, DisplayMemberPath = "Name", SelectedItem = items.FirstOrDefault(e => e.Id == selected) ?? items.First(), Margin = new Thickness(0, 12, 0, 12), MinHeight = 36 }; panel.Children.Add(choices);
        var ok = new Button { Content = "Chọn", IsDefault = true }; ok.Click += (_, _) => DialogResult = true; panel.Children.Add(ok);
    }
}

internal sealed class QaReview : Window
{
    internal JObject Choices { get; } = new();
    internal QaReview(List<JObject> differences)
    {
        Title = "Kiểm tra và duyệt thay đổi từ DWG"; Width = 900; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner; Resources = Theme.Load(); SetResourceReference(BackgroundProperty, "ToolBackground"); SetResourceReference(ForegroundProperty, "ToolText");
        var root = new DockPanel { Margin = new Thickness(20) }; Content = root;
        var hint = new TextBlock { Text = "Chỉ các mục được chọn mới cập nhật vào kho. Mục thiếu trong CAD không tự được khôi phục.\nMở từng mục để đối chiếu thông số trước khi duyệt.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
        var rows = new StackPanel(); var selections = new List<(string id, string action, CheckBox check)>();
        foreach (var d in differences)
        {
            var cad = d["Cad"] as JObject; var app = d["App"] as JObject; bool missing = cad == null||d.Value<string>("Status")=="CAD_SAMPLE_MISSING";
            var check = new CheckBox { Content = (missing ? "Loại khỏi danh mục: " : "Nhận từ CAD: ") + (cad ?? app)!.Value<string>("Name") + " · " + d.Value<string>("Status"), Margin = new Thickness(0, 6, 0, 6), IsChecked = false };
            var info = new TextBox { Text = "Kho hiện tại:\n" + (app?["Values"]?.ToString() ?? "Chưa có") + "\nCAD đã lưu:\n" + (cad?["Values"]?.ToString() ?? "Không còn"), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            rows.Children.Add(check); rows.Children.Add(new Expander { Header = "Xem thông số hai phía", Content = info }); selections.Add((d.Value<string>("Id")!, missing ? "remove" : "cad", check));
        }
        var footer = new StackPanel(); var error = new TextBlock(); footer.Children.Add(error); var ok = new Button { Content = "Duyệt các mục đã chọn", Margin = new Thickness(0, 12, 0, 0) }; footer.Children.Add(ok); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); root.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        ok.Click += (_, _) => { Choices.RemoveAll(); foreach (var item in selections.Where(s => s.check.IsChecked == true)) Choices[item.id] = item.action; if (!Choices.HasValues) { error.Text = "Chưa chọn thay đổi cần duyệt."; return; } DialogResult = true; };
    }
}
