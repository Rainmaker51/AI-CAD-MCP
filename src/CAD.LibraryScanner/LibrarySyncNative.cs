using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil = AICad.Contracts.Json;

[assembly: CommandClass(typeof(AICad.LibraryScanner.LibrarySyncNative))]
namespace AICad.LibraryScanner;

public sealed class LibrarySyncNative
{
    public sealed class LayoutReport
    {
        public int ExistingSamples { get; set; }
        public List<string> MissingSampleIds { get; set; } = new();
    }
    const string ResourceKey = "AICAD_SYNC_RESOURCE";
    const string SampleKey = "AICAD_SYNC_SAMPLE";
    static string Root => Environment.GetEnvironmentVariable("AICAD_LIBRARY_JOB") ?? "";

    [CommandMethod("AICADLIBRARYSYNC")]
    public void Run()
    {
        if (!Path.IsPathRooted(Root) || !File.Exists(Path.Combine(Root, ".aicad-library-job"))) return;
        var previous = HostApplicationServices.WorkingDatabase;
        try
        {
            var request = NativeJson.ReadObject(File.ReadAllText(Path.Combine(Root, "sync-request.json")));
            using var db = new Database(false, true);
            db.ReadDwgFile(Path.Combine(Root, "source.dwg"), FileOpenMode.OpenForReadAndAllShare, true, "");
            db.CloseInput(true);
            HostApplicationServices.WorkingDatabase = db;
            if (db.Insunits != UnitsValue.Millimeters) throw new InvalidOperationException("DWG thư viện phải có đơn vị mm.");
            var managed = request["managed"]?.ToObject<List<LibrarySyncItem>>() ?? new();
            LayoutReport? layoutReport=null;
            if (request.Value<bool>("apply"))
            {
                var writes = request["writes"]!.ToObject<List<LibrarySyncItem>>()!;
                string layoutKind=request.Value<string>("layoutKind")??"";
                if(layoutKind!=""&&((layoutKind!="all"&&!LibrarySyncRules.Kinds.Contains(layoutKind))||writes.Count>0))throw new InvalidOperationException("Yêu cầu bố trí thư viện không hợp lệ.");
                foreach (var item in writes) LibrarySyncRules.Validate(item);
                var groupHints=request["groupNodes"]?.ToObject<List<LibraryFrameNode>>()??new();
                // Native block cloning owns its transaction and runs before the edit transaction.
                foreach (var item in writes.Where(i => i.Kind == "blocks" && i.Values.Value<string>("donorPath") != null))
                    ImportBlock(db, item);
                using var edit = db.TransactionManager.StartTransaction();
                var nodes=LibraryFrames.Nodes(db,edit);
                foreach(var hint in groupHints)LibraryFrames.Ensure(nodes,hint.Kind,hint.Path);
                LibraryFrames.Save(db,edit,nodes);
                var existing = Read(db, edit, managed);
                foreach (var item in writes.OrderBy(i => Array.IndexOf(new[] { "layers", "texts", "dims", "blocks", "hatches" }, i.Kind)))
                    Write(db, edit, item, existing);
                var refreshed = Read(db, edit, managed.Concat(writes).GroupBy(i => i.Id).Select(g => g.Last()).ToList());
                var selected = new List<LibrarySyncItem>();
                foreach (var hint in managed)
                {
                    var found = refreshed.SingleOrDefault(i => i.Id == hint.Id);
                    if (found == null) continue;
                    // Metadata changes from the app are written only for selected app-to-CAD items.
                    var write = writes.SingleOrDefault(i => i.Id == found.Id);
                    if (write != null) { found.Group = write.Group; found.Purpose = write.Purpose; found.Notes = write.Notes; }
                    if (layoutKind == "all" || layoutKind == found.Kind) { found.Group = hint.Group; found.GroupEditedInCad = false; }
                    var resource = Resolve(db, edit, found);
                    Tag(resource, edit, ResourceKey, found);
                    selected.Add(found);
                }
                if(layoutKind!="")layoutReport=ArrangeExistingDrawing(db,edit,selected,layoutKind);
                else Gallery(db, edit, selected, request);
                edit.Commit();
                db.SaveAs(Path.Combine(Root, "synced.dwg"), DwgVersion.AC1032);
            }
            using var read = db.TransactionManager.StartOpenCloseTransaction();
            var items = Read(db, read, managed);
            if (request.Value<bool>("apply") || request.Value<bool>("previews")) HatchPreviews(db, read, items.Where(i => i.Kind == "hatches" && (request.Value<bool>("previews") || managed.Any(m => m.Id == i.Id))));
            JsonUtil.Atomic(Path.Combine(Root, "sync-result.json"), new { items, layout=layoutReport, sourceHash = JsonUtil.FileHash(Path.Combine(Root, request.Value<bool>("apply") ? "synced.dwg" : "source.dwg")) });
        }
        catch (System.Exception error)
        {
            JsonUtil.Atomic(Path.Combine(Root, "sync-error.json"), new { message = error.Message, detail = error.ToString() });
        }
        finally { HostApplicationServices.WorkingDatabase = previous; }
    }

    static LibrarySyncItem? Metadata(DBObject obj, Transaction tx, string key)
    {
        if (obj.ExtensionDictionary.IsNull) return null;
        var dictionary = (DBDictionary)tx.GetObject(obj.ExtensionDictionary, OpenMode.ForRead);
        if (!dictionary.Contains(key)) return null;
        var record = (Xrecord)tx.GetObject(dictionary.GetAt(key), OpenMode.ForRead);
        string json = string.Concat(record.Data.AsArray().Select(v => v.Value.ToString()));
        return JsonUtil.Read<LibrarySyncItem>(json);
    }

    static void Tag(DBObject obj, Transaction tx, string key, LibrarySyncItem item)
    {
        if (!obj.IsWriteEnabled) obj.UpgradeOpen();
        if (obj.ExtensionDictionary.IsNull) obj.CreateExtensionDictionary();
        var dictionary = (DBDictionary)tx.GetObject(obj.ExtensionDictionary, OpenMode.ForWrite);
        Xrecord record;
        if (dictionary.Contains(key)) record = (Xrecord)tx.GetObject(dictionary.GetAt(key), OpenMode.ForWrite);
        else { record = new Xrecord(); dictionary.SetAt(key, record); tx.AddNewlyCreatedDBObject(record, true); }
        var stored = item.Copy();
        if (key == ResourceKey) stored.Handle = obj.Handle.ToString();
        string json = JsonUtil.Write(stored);
        record.Data = new ResultBuffer(Enumerable.Range(0, (json.Length + 199) / 200)
            .Select(i => new TypedValue((int)DxfCode.Text, json.Substring(i * 200, Math.Min(200, json.Length - i * 200)))).ToArray());
    }

    static LibrarySyncItem Item(DBObject obj, Transaction tx, string kind, string name, JObject values, List<LibrarySyncItem> hints)
    {
        var item = Metadata(obj, tx, ResourceKey) ?? hints.FirstOrDefault(i=>i.Kind==kind&&i.Handle!=""&&i.Handle.Equals(obj.Handle.ToString(),StringComparison.OrdinalIgnoreCase))?.Copy() ?? hints.FirstOrDefault(i => i.Kind == kind &&
            (kind == "hatches" ? i.Handle == obj.Handle.ToString() || HatchMatch(i.Values, values) : string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))?.Copy()
            ?? new LibrarySyncItem { Id = JsonUtil.Hash(new[] { kind, name }).Substring(0, 32), Kind = kind };
        item.Kind = kind; item.Name = name; item.Handle = obj.Handle.ToString(); item.Values = values;
        if (item.OriginalName == "") item.OriginalName = name;
        return item;
    }

    static JObject ColorValues(Entity e) => JObject.FromObject(new { color = (int)e.ColorIndex,
        rgb = e.Color.ColorMethod == ColorMethod.ByColor ? (int?)(e.Color.Red << 16 | e.Color.Green << 8 | e.Color.Blue) : null });

    static bool HatchMatch(JObject a, JObject b) => new[] { "pattern", "type", "layer" }.All(k => string.Equals(a.Value<string>(k), b.Value<string>(k), StringComparison.OrdinalIgnoreCase)) &&
        new[] { "scale", "angle", "color", "spacing" }.All(k => a[k] != null && b[k] != null && Math.Abs(a.Value<double>(k)-b.Value<double>(k)) < 1e-7) && a.Value<bool>("doublePattern") == b.Value<bool>("doublePattern");
    public static JObject HatchValues(Hatch h)
    {
        var values=JObject.FromObject(new{pattern=h.PatternName,type=h.PatternType.ToString(),scale=h.PatternScale,angle=h.PatternAngle,layer=h.Layer,spacing=h.PatternSpace,doublePattern=h.PatternDouble});values.Merge(ColorValues(h));return values;
    }

    static List<LibrarySyncItem> Read(Database db, Transaction tx, List<LibrarySyncItem> hints)
    {
        var result = new List<LibrarySyncItem>();
        foreach (ObjectId id in (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead))
        {
            var l = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (l.IsDependent) continue;
            var type = (LinetypeTableRecord)tx.GetObject(l.LinetypeObjectId, OpenMode.ForRead);
            result.Add(Item(l, tx, "layers", l.Name, JObject.FromObject(new
            {
                color = (int)l.Color.ColorIndex, rgb = l.Color.ColorMethod == ColorMethod.ByColor ? (int?)(l.Color.Red << 16 | l.Color.Green << 8 | l.Color.Blue) : null,
                lineWeight = (int)l.LineWeight, lineType = type.Name, plottable = l.IsPlottable,
                dashes = Enumerable.Range(0, type.NumDashes).Select(type.DashLengthAt).ToArray(), patternLength = type.PatternLength
            }), hints));
        }
        foreach (ObjectId id in (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead))
        {
            var s = (TextStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (s.IsDependent) continue;
            result.Add(Item(s, tx, "texts", s.Name, JObject.FromObject(new { font = Path.GetFileName(s.FileName), bigFont = Path.GetFileName(s.BigFontFileName),
                height = s.TextSize, widthFactor = s.XScale, obliqueAngle = s.ObliquingAngle, vertical = s.IsVertical }), hints));
        }
        foreach (ObjectId id in (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForRead))
        {
            var s = (DimStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (s.IsDependent) continue;
            result.Add(Item(s, tx, "dims", s.Name, JObject.FromObject(new
            {
                textStyle = ((TextStyleTableRecord)tx.GetObject(s.Dimtxsty, OpenMode.ForRead)).Name,
                textHeight = s.Dimtxt, arrowSize = s.Dimasz, scale = s.Dimscale, linearFactor = s.Dimlfac, precision = s.Dimdec,
                extensionOffset = s.Dimexo, extensionBeyond = s.Dimexe, baselineSpacing = s.Dimdli, textGap = s.Dimgap,
                rounding = s.Dimrnd, unitFormat = s.Dimlunit, angularPrecision = s.Dimadec, zeroSuppression = s.Dimzin,
                annotative = s.Annotative == AnnotativeStates.True
            }), hints));
        }
        var hatchKeys = new HashSet<string>();
        var blockIds = ((BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead)).Cast<ObjectId>().ToArray();
        foreach (ObjectId id in blockIds)
        {
            var b = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (b.IsFromExternalReference || b.IsFromOverlayReference) continue;
            if (!b.IsLayout && !b.IsAnonymous)
            {
                var properties = new JArray();
                if(b.Name.StartsWith("AICAD_LIB_",StringComparison.OrdinalIgnoreCase))continue;
                if(File.Exists(Path.Combine(Root,".aicad-library-job")))File.WriteAllText(Path.Combine(Root,"progress.txt"),"Read block " + b.Name);
                // AutoCAD 2021 requires a database-resident reference to evaluate dynamic properties safely.
                var bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var probe = new BlockReference(Point3d.Origin, b.ObjectId);
                ms.AppendEntity(probe); tx.AddNewlyCreatedDBObject(probe, true);
                if (probe.IsDynamicBlock) foreach (DynamicBlockReferenceProperty p in probe.DynamicBlockReferencePropertyCollection)
                    properties.Add(JObject.FromObject(new { name = p.PropertyName, value = p.Value, valueType = p.Value?.GetType().Name, visible = p.VisibleInCurrentVisibilityState, readOnly = p.ReadOnly, units = p.UnitsType.ToString(), allowed = p.GetAllowedValues() }));
                var attributes = b.Cast<ObjectId>().Select(child => tx.GetObject(child, OpenMode.ForRead)).OfType<AttributeDefinition>()
                    .Select(a => new { tag = a.Tag, prompt = a.Prompt, value = a.TextString, constant = a.Constant }).ToArray();
                var shape = BlockShape(b, tx, new HashSet<ObjectId>(), 0);
                Extents3d extents;
                try { extents = probe.GeometricExtents; }
                catch (Autodesk.AutoCAD.Runtime.Exception) { probe.Erase(); continue; }
                result.Add(Item(b, tx, "blocks", b.Name, JObject.FromObject(new
                {
                    units = b.Units.ToString(), originX = b.Origin.X, originY = b.Origin.Y,
                    minX = extents.MinPoint.X, minY = extents.MinPoint.Y, maxX = extents.MaxPoint.X, maxY = extents.MaxPoint.Y,
                    dynamic = b.IsDynamicBlock, properties, attributes, geometryHash = JsonUtil.Hash(shape)
                }), hints));
                probe.Erase();
            }
            foreach (ObjectId child in b)
            {
                if (tx.GetObject(child, OpenMode.ForRead) is not Hatch h) continue;
                var resourceTag = Metadata(h, tx, ResourceKey);
                if (Metadata(h, tx, SampleKey) != null && (resourceTag == null || resourceTag.Handle != h.Handle.ToString())) continue;
                var values = JObject.FromObject(new { pattern = h.PatternName, type = h.PatternType.ToString(), scale = h.PatternScale,
                    angle = h.PatternAngle, layer = h.Layer, spacing = h.PatternSpace, doublePattern = h.PatternDouble });
                values.Merge(ColorValues(h));
                string key = JsonUtil.Hash(values);
                var metadata = Metadata(h, tx, ResourceKey);
                var known = hints.FirstOrDefault(i => i.Kind == "hatches" && (i.Handle == h.Handle.ToString() || HatchMatch(i.Values, values)));
                if (metadata == null && known == null && !hatchKeys.Add(key)) continue;
                hatchKeys.Add(key);
                var item = Item(h, tx, "hatches", known?.Name ?? h.PatternName + " · " + h.PatternScale + " · " + Math.Round(h.PatternAngle * 180 / Math.PI, 2) + "°", values, hints);
                if (metadata == null && known == null) item.Id = key.Substring(0, 32);
                if (result.All(i => i.Id != item.Id)) result.Add(item);
            }
        }
        // Editable metadata in generated labels is read back into the same stable resource.
        var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        var model = (BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in model)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not MText label) continue;
            var tagged = Metadata(label, tx, SampleKey);
            var resource = tagged == null ? null : result.FirstOrDefault(i => i.Id == tagged.Id);
            if (resource == null) continue;
            foreach (string line in label.Text.Replace("\r", "").Split('\n'))
            {
                if (line.StartsWith("Nhóm | ")) resource.Group = line.Substring(7).Trim();
                if (line.StartsWith("Công dụng | ")) resource.Purpose = line.Substring(12).Trim();
                if (line.StartsWith("Ghi chú | ")) resource.Notes = line.Substring(10).Trim();
            }
        }
        if (result.Count > 3000) throw new InvalidOperationException("Nguồn vượt 3.000 tài nguyên, hãy chia nhỏ DWG.");
        return result;
    }

    static JArray BlockShape(BlockTableRecord block, Transaction tx, HashSet<ObjectId> stack, int depth)
    {
        if (depth > 16 || !stack.Add(block.ObjectId)) throw new InvalidOperationException("Block lồng có vòng tham chiếu hoặc quá sâu.");
        var values = new JArray();
        foreach (ObjectId id in block)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            var o = JObject.FromObject(new { type = e.GetType().Name, layer = e.Layer, color = e.ColorIndex, visible = e.Visible, lineType=e.Linetype, lineTypeScale=e.LinetypeScale, lineWeight=(int)e.LineWeight });
            double[] P(Point3d p) => new[] { p.X, p.Y, p.Z };
            if (e is Line l) o["points"] = JArray.FromObject(new[] { P(l.StartPoint), P(l.EndPoint) });
            else if (e is Polyline p) { o["vertices"] = JArray.FromObject(Enumerable.Range(0, p.NumberOfVertices).Select(i => new[] { p.GetPoint2dAt(i).X, p.GetPoint2dAt(i).Y, p.GetBulgeAt(i), p.GetStartWidthAt(i), p.GetEndWidthAt(i) })); o["closed"] = p.Closed; }
            else if (e is BlockReference b) { o["position"] = JArray.FromObject(P(b.Position)); o["rotation"] = b.Rotation; o["scale"] = JArray.FromObject(new[] { b.ScaleFactors.X, b.ScaleFactors.Y, b.ScaleFactors.Z }); o["children"] = BlockShape((BlockTableRecord)tx.GetObject(b.BlockTableRecord, OpenMode.ForRead), tx, stack, depth + 1); }
            else if (e is DBText t) { o["text"] = t.TextString; o["position"] = JArray.FromObject(P(t.Position)); o["height"] = t.Height; o["rotation"] = t.Rotation; }
            else if (e is MText m) { o["text"] = m.Contents; o["position"] = JArray.FromObject(P(m.Location)); o["height"] = m.TextHeight; }
            else if (e is Arc a) { o["center"] = JArray.FromObject(P(a.Center)); o["radius"] = a.Radius; o["start"] = a.StartAngle; o["end"] = a.EndAngle; }
            else if (e is Circle c) { o["center"] = JArray.FromObject(P(c.Center)); o["radius"] = c.Radius; }
            else if (e is Solid solid) o["points"]=JArray.FromObject(Enumerable.Range(0,4).Select(i=>P(solid.GetPointAt((short)i))));
            else { try { o["min"] = JArray.FromObject(P(e.GeometricExtents.MinPoint)); o["max"] = JArray.FromObject(P(e.GeometricExtents.MaxPoint)); } catch (Autodesk.AutoCAD.Runtime.Exception) { } }
            if (e is Hatch hatch)
            {
                o["pattern"] = hatch.PatternName; o["scale"] = hatch.PatternScale; o["angle"] = hatch.PatternAngle;
                var loops=new JArray();
                for(int i=0;i<hatch.NumberOfLoops;i++)
                {
                    var loop=hatch.GetLoopAt(i);
                    if(loop.IsPolyline)loops.Add(JArray.FromObject(loop.Polyline.Cast<BulgeVertex>().Select(v=>new[]{v.Vertex.X,v.Vertex.Y,v.Bulge})));
                    else loops.Add(loop.Curves.Count.ToString());
                }
                o["loops"]=loops;
            }
            values.Add(o);
        }
        stack.Remove(block.ObjectId);
        return values;
    }

    static DBObject Resolve(Database db, Transaction tx, LibrarySyncItem item)
    {
        if (!string.IsNullOrEmpty(item.Handle))
        {
            var id = db.GetObjectId(false, new Handle(Convert.ToInt64(item.Handle, 16)), 0);
            if (!id.IsErased) return tx.GetObject(id, OpenMode.ForRead);
        }
        throw new InvalidOperationException("Không tìm thấy tài nguyên " + item.Name);
    }

    static Color ColorOf(JObject values) => values.Value<int?>("rgb") is int rgb
        ? Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
        : Color.FromColorIndex(ColorMethod.ByAci, (short)(values.Value<int?>("color") ?? 7));

    static void ImportBlock(Database target, LibrarySyncItem item)
    {
        string path = item.Values.Value<string>("donorPath")!;
        using var source = new Database(false, true);
        source.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, ""); source.CloseInput(true);
        using var tx = source.TransactionManager.StartTransaction();
        var table = (BlockTable)tx.GetObject(source.BlockTableId, OpenMode.ForRead);
        string name = item.Values.Value<string>("donorName") ?? item.Name;
        if (!table.Has(name)) throw new InvalidOperationException("DWG nhập không có block " + name);
        var definition = (BlockTableRecord)tx.GetObject(table[name], OpenMode.ForWrite);
        string temporaryName = "AICAD_SYNC_IMPORT_" + Guid.NewGuid().ToString("N");
        definition.Name = temporaryName;
        var mapping = new IdMapping();
        source.WblockCloneObjects(new ObjectIdCollection(new[] { definition.ObjectId }), target.BlockTableId, mapping, DuplicateRecordCloning.Ignore, false);
        using var edit = target.TransactionManager.StartTransaction();
        var targetTable = (BlockTable)edit.GetObject(target.BlockTableId, OpenMode.ForRead);
        var imported = (BlockTableRecord)edit.GetObject(mapping[definition.ObjectId].Value, OpenMode.ForWrite);
        ObjectId replaced = ObjectId.Null;
        string previous = string.IsNullOrEmpty(item.OriginalName) ? item.Name : item.OriginalName;
        if (targetTable.Has(previous))
        {
            var existing = (BlockTableRecord)edit.GetObject(targetTable[previous], OpenMode.ForWrite);
            var meta = Metadata(existing, edit, ResourceKey);
            if ((meta?.Id ?? JsonUtil.Hash(new[] { "blocks", previous }).Substring(0,32)) != item.Id)
                throw new InvalidOperationException("Tên block đã có trong CAD. Đọc và chọn mục đó để cập nhật.");
            replaced = existing.ObjectId; existing.Name = "AICAD_SYNC_PREVIOUS_" + Guid.NewGuid().ToString("N");
        }
        imported.Name = item.Name;
        if (!replaced.IsNull)
        {
            foreach (ObjectId bid in targetTable.Cast<ObjectId>().ToArray())
            {
                var owner = (BlockTableRecord)edit.GetObject(bid, OpenMode.ForRead);
                foreach (ObjectId id in owner.Cast<ObjectId>().ToArray())
                {
                    if (edit.GetObject(id, OpenMode.ForRead) is not BlockReference reference ||
                        reference.BlockTableRecord != replaced && (!reference.IsDynamicBlock || reference.DynamicBlockTableRecord != replaced)) continue;
                    var values = reference.IsDynamicBlock ? reference.DynamicBlockReferencePropertyCollection.Cast<DynamicBlockReferenceProperty>().Where(p=>!p.ReadOnly).ToDictionary(p=>p.PropertyName,p=>p.Value) : new Dictionary<string,object>();
                    reference.UpgradeOpen();reference.BlockTableRecord = imported.ObjectId;
                    if (reference.IsDynamicBlock) foreach (DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
                        if (!property.ReadOnly && values.TryGetValue(property.PropertyName,out object value) && value.GetType()==property.Value.GetType()) property.Value=value;
                }
            }
            var purge = new ObjectIdCollection(new[] { replaced });target.Purge(purge);
            foreach (ObjectId id in purge)edit.GetObject(id,OpenMode.ForWrite).Erase();
        }
        Tag(imported, edit, ResourceKey, item);
        edit.Commit();
    }

    static string ResolveFontFile(Database db, string font, string styleName)
    {
        string name = Path.GetFileName(font);
        if (!string.IsNullOrWhiteSpace(font) && Path.IsPathRooted(font) && File.Exists(font)) return font;
        foreach (string folder in new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
            Path.Combine(Path.GetDirectoryName(typeof(Database).Assembly.Location)!, "Fonts") })
        {
            string candidate = Path.Combine(folder, name);
            if (!string.IsNullOrWhiteSpace(name) && File.Exists(candidate)) return candidate;
        }
        try
        {
            string found = HostApplicationServices.Current.FindFile(name, db, FindFileHint.FontFile);
            if (File.Exists(found)) return found;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception) { }
        throw new InvalidOperationException("FONT_MISSING: Text style " + styleName + " cần font " + name +
            ". Cài font trên máy này hoặc chọn đúng file font trong bản nháp rồi đồng bộ lại.");
    }

    static void Write(Database db, Transaction tx, LibrarySyncItem item, List<LibrarySyncItem> existing)
    {
        var old = existing.SingleOrDefault(i => i.Id == item.Id);
        var v = item.Values;
        DBObject obj;
        if (item.Kind == "layers")
        {
            var table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForWrite);
            var layer = old != null ? (LayerTableRecord)Resolve(db, tx, old) : new LayerTableRecord { Name = item.Name };
            if (old == null) { table.Add(layer); tx.AddNewlyCreatedDBObject(layer, true); } else { layer.UpgradeOpen(); layer.Name = item.Name; }
            var types = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            string type = v.Value<string>("lineType")!;
            if (!types.Has(type)) { db.LoadLineTypeFile(type, "acadiso.lin"); }
            layer.Color = ColorOf(v); layer.LineWeight = (LineWeight)v.Value<int>("lineWeight"); layer.LinetypeObjectId = types[type]; layer.IsPlottable = v.Value<bool>("plottable"); obj = layer;
        }
        else if (item.Kind == "texts")
        {
            var table = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForWrite);
            var style = old != null ? (TextStyleTableRecord)Resolve(db, tx, old) : new TextStyleTableRecord { Name = item.Name };
            if (old == null) { table.Add(style); tx.AddNewlyCreatedDBObject(style, true); } else { style.UpgradeOpen(); style.Name = item.Name; }
            string font = v.Value<string>("font")!;
            string installed = ResolveFontFile(db, font, item.Name);
            string bigFont = v.Value<string>("bigFont") ?? "";
            style.FileName = installed; style.BigFontFileName = string.IsNullOrWhiteSpace(bigFont) ? "" : ResolveFontFile(db, bigFont, item.Name);
            style.TextSize = v.Value<double>("height"); style.XScale = v.Value<double>("widthFactor"); style.ObliquingAngle = v.Value<double>("obliqueAngle"); style.IsVertical = v.Value<bool>("vertical"); obj = style;
        }
        else if (item.Kind == "dims")
        {
            var table = (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForWrite);
            var style = old != null ? (DimStyleTableRecord)Resolve(db, tx, old) : new DimStyleTableRecord { Name = item.Name };
            if (old == null) { style.CopyFrom((DimStyleTableRecord)tx.GetObject(db.Dimstyle, OpenMode.ForRead)); style.Name = item.Name; table.Add(style); tx.AddNewlyCreatedDBObject(style, true); } else { style.UpgradeOpen(); style.Name = item.Name; }
            var texts = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            style.Dimtxsty = texts[v.Value<string>("textStyle")!]; style.Annotative = v.Value<bool>("annotative") ? AnnotativeStates.True : AnnotativeStates.False;
            style.Dimtxt = v.Value<double>("textHeight"); style.Dimasz = v.Value<double>("arrowSize"); style.Dimscale = v.Value<bool>("annotative") ? 0 : v.Value<double>("scale"); style.Dimlfac = v.Value<double>("linearFactor"); style.Dimdec = v.Value<int>("precision");
            style.Dimexo = v.Value<double>("extensionOffset"); style.Dimexe = v.Value<double>("extensionBeyond"); style.Dimdli = v.Value<double>("baselineSpacing"); style.Dimgap = v.Value<double>("textGap"); style.Dimrnd = v.Value<double>("rounding"); style.Dimlunit = v.Value<int>("unitFormat"); style.Dimadec = v.Value<int>("angularPrecision"); style.Dimzin = v.Value<int>("zeroSuppression"); obj = style;
        }
        else if (item.Kind == "blocks")
        {
            var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            string name = v.Value<string>("donorHash") != null ? item.Name : old?.Name ?? item.Name;
            if (!table.Has(name)) throw new InvalidOperationException("BLOCK_SOURCE_MISSING: Thiếu định nghĩa block " + name +
                ". Nhập DWG chứa block này trước, hoặc chọn Giữ để ra sau để đồng bộ các tài nguyên khác.");
            var block = (BlockTableRecord)tx.GetObject(table[name], OpenMode.ForWrite); block.Name = item.Name; obj = block;
        }
        else
        {
            Hatch hatch;
            if (old != null) { hatch = (Hatch)Resolve(db, tx, old); hatch.UpgradeOpen(); }
            else
            {
                var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                hatch = new Hatch(); model.AppendEntity(hatch); tx.AddNewlyCreatedDBObject(hatch, true);
                var loop = new HatchLoop(HatchLoopTypes.External | HatchLoopTypes.Polyline);
                foreach (var p in new[] { new Point2d(0,0), new Point2d(2000,0), new Point2d(2000,1200), new Point2d(0,1200), new Point2d(0,0) }) loop.Polyline.Add(new BulgeVertex(p, 0));
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID"); hatch.AppendLoop(loop);
            }
            hatch.Layer = v.Value<string>("layer") ?? "0"; hatch.Color = ColorOf(v);
            var type = (HatchPatternType)Enum.Parse(typeof(HatchPatternType), v.Value<string>("type") ?? "PreDefined");
            hatch.SetHatchPattern(type, v.Value<string>("pattern")); hatch.PatternScale = v.Value<double>("scale"); hatch.PatternAngle = v.Value<double>("angle");
            if (type == HatchPatternType.UserDefined) { hatch.PatternSpace = v.Value<double>("spacing"); hatch.PatternDouble = v.Value<bool>("doublePattern"); }
            hatch.EvaluateHatch(true); obj = hatch;
        }
        item.GroupEditedInCad = false;
        Tag(obj, tx, ResourceKey, item);
    }

    public static List<LibrarySyncItem> ReadDrawing(Database db, Transaction tx) => Read(db, tx, new List<LibrarySyncItem>());
    public static void RebindSnapshotHandle(DBObject obj,Transaction tx)
    {
        var item=Metadata(obj,tx,ResourceKey);if(item==null)return;
        Tag(obj,tx,ResourceKey,item);
    }
    public static List<LibrarySyncItem> ReadDrawing(Database db, Transaction tx, List<LibrarySyncItem> hints) => Read(db, tx, hints);
    public static void EditResource(Database db, Transaction tx, LibrarySyncItem item, List<LibrarySyncItem> hints)
    {
        LibrarySyncRules.Validate(item);
        var existing = Read(db, tx, hints);
        Write(db, tx, item, existing);
        // Only a genuinely new resource gets a new presentation sample. Editing a
        // surviving definition must never restore an insertion erased by the user.
        if(existing.Any(i=>i.Id==item.Id))return;
        var created=Read(db,tx,hints.Concat(new[]{item}).ToList()).Single(i=>i.Id==item.Id);
        var resource=Resolve(db,tx,created);
        Entity sample=resource switch{
            LayerTableRecord layer=>new Line(Point3d.Origin,new Point3d(5200,0,0)){LayerId=layer.ObjectId},
            BlockTableRecord block=>new BlockReference(Point3d.Origin,block.ObjectId),
            TextStyleTableRecord text=>new DBText{TextString="Aa Bb 0123456789",TextStyleId=text.ObjectId,Height=text.TextSize>0?text.TextSize:200},
            DimStyleTableRecord dim=>new RotatedDimension(0,Point3d.Origin,new Point3d(3000,0,0),new Point3d(0,800,0),"",dim.ObjectId),
            Hatch hatch=>hatch,
            _=>throw new InvalidOperationException("Không tạo được mẫu mới.")};
        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
        if(sample.ObjectId.IsNull){model.AppendEntity(sample);tx.AddNewlyCreatedDBObject(sample,true);}
        if(sample is BlockReference reference)
            foreach(ObjectId child in (BlockTableRecord)tx.GetObject(reference.BlockTableRecord,OpenMode.ForRead))
                if(tx.GetObject(child,OpenMode.ForRead) is AttributeDefinition attribute&&!attribute.Constant){var value=new AttributeReference();value.SetAttributeFromBlock(attribute,reference.BlockTransform);value.TextString=attribute.TextString;reference.AttributeCollection.AppendAttribute(value);tx.AddNewlyCreatedDBObject(value,true);}
        if(sample is Dimension dimension)dimension.RecomputeDimensionBlock(true);
        MarkSample(sample,tx,created);LibraryFrames.AddCell(db,tx,created,sample);
        var nodes=LibraryFrames.Nodes(db,tx);var node=LibraryFrames.Ensure(nodes,created.Kind,created.Group);LibraryFrames.Save(db,tx,nodes);
        var displayed=Read(db,tx,hints.Concat(new[]{created}).ToList()).Where(i=>ModelHasSample(db,tx,i.Id)).ToList();
        LibraryFrames.AppendToGroup(db,tx,node,displayed,new(){created});LibraryFrames.Clear(db,tx);LibraryFrames.Build(db,tx,displayed);
    }
    static bool ModelHasSample(Database db,Transaction tx,string id)=>LibraryFrames.Model(db,tx).Any(e=>SampleMetadata(e,tx)?.Id==id&&e is not MText&&e is not Polyline);
    public static LibrarySyncItem? SampleMetadata(DBObject obj, Transaction tx) => Metadata(obj, tx, SampleKey);
    public static void MarkSample(Entity entity, Transaction tx, LibrarySyncItem item) => Tag(entity,tx,SampleKey,new LibrarySyncItem{Id=item.Id,Kind=item.Kind,Name=item.Name,Handle=entity.Handle.ToString()});
    public static bool Managed(Database db, Transaction tx, LibrarySyncItem item) => Metadata(Resolve(db, tx, item), tx, ResourceKey) != null;
    public static void AssignGroup(Database db, Transaction tx, LibrarySyncItem item, string group, Entity? sample = null)
    {
        item.Group = group; item.GroupEditedInCad = true;
        Tag(Resolve(db, tx, item), tx, ResourceKey, item);
        if(sample!=null)MarkSample(sample,tx,item);
        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
        foreach(ObjectId id in (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead))
        {
            if(tx.GetObject(id,OpenMode.ForRead) is not MText label||SampleMetadata(label,tx)?.Id!=item.Id)continue;
            var lines=label.Text.Replace("\r","").Split('\n');
            label.UpgradeOpen();label.Contents=string.Join("\\P",lines.Select(line=>Safe(line.StartsWith("Nhóm | ")?"Nhóm | "+group:line)));
        }
    }
    public static void ArrangeDrawing(Database db, Transaction tx, List<LibrarySyncItem> items) => Gallery(db,tx,items,new JObject{["keepSamples"]=true});

    /// <summary>Regroup existing samples only. A surviving definition is not permission to recreate an erased insertion.</summary>
    public static LayoutReport ArrangeExistingDrawing(Database db,Transaction tx,List<LibrarySyncItem> items,string kind)
    {
        if(kind!="all"&&!LibrarySyncRules.Kinds.Contains(kind))throw new CadException("SYNC_LAYOUT","Loại bố trí không hợp lệ.");
        var report=new LayoutReport();
        foreach(var item in items.Where(i=>kind=="all"||i.Kind==kind))
        {
            var glyph=LibraryFrames.Model(db,tx).Where(e=>SampleMetadata(e,tx)?.Id==item.Id).ToArray();
            var sample=glyph.FirstOrDefault(e=>item.Kind=="blocks"&&e is BlockReference||item.Kind=="layers"&&e is Line||
                item.Kind=="hatches"&&e is Hatch||item.Kind=="texts"&&e is DBText||item.Kind=="dims"&&e is Dimension);
            if(sample==null)
            {
                report.MissingSampleIds.Add(item.Id);
                // Remove only our orphaned annotation, never restore the missing sample or erase its definition.
                foreach(var annotation in glyph.Where(e=>e is MText||e is Polyline)){annotation.UpgradeOpen();annotation.Erase();}
                continue;
            }
            report.ExistingSamples++;
            LibraryFrames.AddCell(db,tx,item,sample);
            foreach(var label in LibraryFrames.Model(db,tx).OfType<MText>().Where(e=>SampleMetadata(e,tx)?.Id==item.Id))
            {
                var lines=label.Text.Replace("\r","").Split('\n').ToList();
                if(lines.Count>0)lines[0]=item.Name;
                foreach(var field in new[]{new{Key="Nhóm | ",Value=item.Group},new{Key="Công dụng | ",Value=item.Purpose},new{Key="Ghi chú | ",Value=item.Notes}})
                {
                    int index=lines.FindIndex(line=>line.StartsWith(field.Key,StringComparison.Ordinal));
                    if(index>=0)lines[index]=field.Key+field.Value;else lines.Add(field.Key+field.Value);
                }
                label.UpgradeOpen();label.Contents=string.Join("\\P",lines.Select(Safe));
            }
        }
        // Keep unrelated kinds untouched in the frame inputs. Missing samples in the selected kind are not counted.
        var missing=new HashSet<string>(report.MissingSampleIds);
        var displayed=items.Where(i=>!missing.Contains(i.Id)).ToList();
        LibraryFrames.Clear(db,tx);LibraryFrames.Build(db,tx,displayed);
        return report;
    }

    static void Gallery(Database db, Transaction tx, List<LibrarySyncItem> items, JObject request)
    {
        if(!LibraryFrames.Model(db,tx).Any(e=>SampleMetadata(e,tx)!=null)){
            CreateGallery(db,tx,items,request);return;
        }
        var ids=items.Select(i=>i.Id).ToHashSet();
        foreach(var entity in LibraryFrames.Model(db,tx).ToArray()){
            var tag=SampleMetadata(entity,tx);
            if(tag==null||ids.Contains(tag.Id))continue;
            var resource=Metadata(entity,tx,ResourceKey);
            if(resource!=null&&resource.Handle==entity.Handle.ToString())continue;
            entity.UpgradeOpen();entity.Erase();
        }
        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
        var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
        var added=new List<LibrarySyncItem>();
        foreach(var item in items){
            var glyph=LibraryFrames.Model(db,tx).Where(e=>SampleMetadata(e,tx)?.Id==item.Id).ToArray();
            var sample=glyph.FirstOrDefault(e=>item.Kind=="blocks"&&e is BlockReference||
                item.Kind=="layers"&&e is Line||item.Kind=="hatches"&&e is Hatch||
                item.Kind=="texts"&&e is DBText||item.Kind=="dims"&&e is Dimension);
            if(sample==null){
                foreach(var stale in glyph){stale.UpgradeOpen();stale.Erase();}
                var resource=Resolve(db,tx,item);
                sample=resource switch{
                    LayerTableRecord layer=>new Line(Point3d.Origin,new Point3d(5200,0,0)){LayerId=layer.ObjectId,ColorIndex=256,LinetypeScale=100},
                    BlockTableRecord block=>new BlockReference(Point3d.Origin,block.ObjectId),
                    TextStyleTableRecord text=>new DBText{TextString="Aa Bb 0123456789 – Mẫu chữ",TextStyleId=text.ObjectId,Height=text.TextSize>0?text.TextSize:200},
                    DimStyleTableRecord dim=>new RotatedDimension(0,Point3d.Origin,new Point3d(3000,0,0),new Point3d(0,800,0),"",dim.ObjectId),
                    Hatch hatch=>(Hatch)hatch,
                    _=>throw new InvalidOperationException("Không tạo được mẫu "+item.Name)
                };
                if(sample.ObjectId.IsNull){model.AppendEntity(sample);tx.AddNewlyCreatedDBObject(sample,true);}
                if(sample is BlockReference reference){
                    var definition=(BlockTableRecord)tx.GetObject(reference.BlockTableRecord,OpenMode.ForRead);
                    foreach(ObjectId child in definition)
                        if(tx.GetObject(child,OpenMode.ForRead) is AttributeDefinition attribute&&!attribute.Constant){
                            var value=new AttributeReference();value.SetAttributeFromBlock(attribute,reference.BlockTransform);value.TextString=attribute.TextString;
                            reference.AttributeCollection.AppendAttribute(value);tx.AddNewlyCreatedDBObject(value,true);
                        }
                }
                if(sample is Dimension dimension)dimension.RecomputeDimensionBlock(true);
                if(sample is Hatch display){display.UpgradeOpen();display.Associative=false;}
                MarkSample(sample,tx,item);
                LibraryFrames.AddCell(db,tx,item,sample);
                added.Add(item);
            }else if(!glyph.OfType<Polyline>().Any(e=>e.Closed&&e.NumberOfVertices==4)){
                LibraryFrames.AddCell(db,tx,item,sample);
            }
            foreach(var label in LibraryFrames.Model(db,tx).OfType<MText>().Where(e=>SampleMetadata(e,tx)?.Id==item.Id)){
                label.UpgradeOpen();
                var lines=label.Text.Replace("\r","").Split('\n').ToList();
                if(lines.Count>0)lines[0]=item.Name;
                foreach(var field in new[]{new{Key="Nhóm | ",Value=item.Group},new{Key="Công dụng | ",Value=item.Purpose},new{Key="Ghi chú | ",Value=item.Notes}}){
                    int index=lines.FindIndex(line=>line.StartsWith(field.Key,StringComparison.Ordinal));
                    if(index>=0)lines[index]=field.Key+field.Value;else lines.Add(field.Key+field.Value);
                }
                label.Contents=string.Join("\\P",lines.Select(Safe));
            }
        }
        var nodes=LibraryFrames.Nodes(db,tx);
        foreach(string kind in LibrarySyncRules.Kinds)LibraryFrames.Ensure(nodes,kind,"");
        foreach(var item in items)LibraryFrames.Ensure(nodes,item.Kind,item.Group);
        LibraryFrames.Save(db,tx,nodes);
        foreach(var group in added.GroupBy(i=>new{i.Kind,i.Group})){
            var node=nodes.Single(n=>n.Kind==group.Key.Kind&&n.Path==group.Key.Group);
            LibraryFrames.AppendToGroup(db,tx,node,items,group.ToList());
        }
        LibraryFrames.Clear(db,tx);
        LibraryFrames.Build(db,tx,items);
    }

    static void CreateGallery(Database db, Transaction tx, List<LibrarySyncItem> items, JObject request)
    {
        var blocks=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
        var model=(BlockTableRecord)tx.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
        var preserved=new Dictionary<string,Entity>();
        if(request.Value<bool>("keepSamples"))foreach(ObjectId id in model)
        {
            if(tx.GetObject(id,OpenMode.ForRead) is not Entity entity)continue;var tag=SampleMetadata(entity,tx);if(tag==null)continue;
            bool correct=tag.Kind=="blocks"&&entity is BlockReference||tag.Kind=="layers"&&entity is Line||tag.Kind=="hatches"&&entity is Hatch||tag.Kind=="texts"&&entity is DBText||tag.Kind=="dims"&&entity is Dimension;
            if(correct&&items.Any(i=>i.Id==tag.Id)&&(!preserved.ContainsKey(tag.Id)||tag.Handle==entity.Handle.ToString()))preserved[tag.Id]=entity;
        }
        var frameWidths=LibraryFrames.Widths(db,tx);LibraryFrames.Clear(db,tx);
        foreach(ObjectId id in model.Cast<ObjectId>().ToArray())
        {
            var obj=tx.GetObject(id,OpenMode.ForRead);
            if(Metadata(obj,tx,SampleKey)==null)continue;
            if(preserved.Values.Any(e=>e.ObjectId==id))continue;
            var resourceTag=Metadata(obj,tx,ResourceKey);
            if(resourceTag!=null&&resourceTag.Handle==obj.Handle.ToString())continue;
            obj.UpgradeOpen();obj.Erase();
        }
        var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead);
        ObjectId labelLayer=layers.Has("QA_KT_Text")?layers["QA_KT_Text"]:layers["0"];
        ObjectId frameLayer=layers.Has("QA_KH_Khung Ten")?layers["QA_KH_Khung Ten"]:layers["0"];
        var styles=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForRead);
        ObjectId labelStyle=styles.Has("QA_Tahoma")?styles["QA_Tahoma"]:db.Textstyle;
        var views=(ViewTable)tx.GetObject(db.ViewTableId,OpenMode.ForWrite);
        foreach(ObjectId id in views.Cast<ObjectId>().ToArray())
        {
            var view=(ViewTableRecord)tx.GetObject(id,OpenMode.ForRead);
            if(Metadata(view,tx,SampleKey)==null)continue;
            view.UpgradeOpen();view.Erase();
        }
        double sectionX=request.Value<double?>("layoutX")??100000,top=request.Value<double?>("layoutY")??0;
        const double targetWidth=54000,gap=450,sectionGap=6000;
        // Place managed sections beyond existing, untagged content so expansion cannot cover the source archive.
        foreach(ObjectId id in model)
        {
            if(tx.GetObject(id,OpenMode.ForRead) is not Entity existing||Metadata(existing,tx,SampleKey)!=null||items.Any(i=>i.Kind=="hatches"&&i.Handle==existing.Handle.ToString()))continue;
            try{sectionX=Math.Max(sectionX,existing.GeometricExtents.MaxPoint.X+sectionGap);}catch(Autodesk.AutoCAD.Runtime.Exception){}
        }
        double layoutLeft=sectionX,layoutBottom=top;
        void Append(Entity entity,LibrarySyncItem item)
        {
            model.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);
            Tag(entity,tx,SampleKey,new LibrarySyncItem{Id=item.Id,Kind=item.Kind,Name=item.Name});
        }
        void View(string name,double left,double right,double upper,double lower,LibrarySyncItem tag)
        {
            ViewTableRecord view;
            if(views.Has(name))view=(ViewTableRecord)tx.GetObject(views[name],OpenMode.ForWrite);
            else{view=new ViewTableRecord{Name=name};views.Add(view);tx.AddNewlyCreatedDBObject(view,true);}
            view.CenterPoint=new Point2d((left+right)/2,(upper+lower)/2);view.Width=right-left+1000;view.Height=Math.Max(3000,upper-lower+1500);view.Target=Point3d.Origin;view.ViewDirection=Vector3d.ZAxis;
            Tag(view,tx,SampleKey,tag);
        }
        var kinds=new[]{"BLOCK","LAYER","HATCH","TEXT","DIM"};
        for(int section=0;section<LibrarySyncRules.Kinds.Length;section++)
        {
            string kind=LibrarySyncRules.Kinds[section];double y=top,sectionWidth=targetWidth;
            var heading=new LibrarySyncItem{Id=JsonUtil.Hash(new[]{"section",kind}).Substring(0,32),Kind=kind,Name=kinds[section]};
            Append(new MText{Contents=(section+1).ToString("00")+" | "+kinds[section],Location=new Point3d(sectionX,y,0),TextHeight=650,Width=targetWidth,LayerId=labelLayer,TextStyleId=labelStyle},heading);
            y-=1800;
            var groups=items.Where(i=>i.Kind==kind).OrderBy(i=>i.Group,StringComparer.OrdinalIgnoreCase).ThenBy(i=>i.Name,StringComparer.OrdinalIgnoreCase).GroupBy(i=>i.Group).ToArray();
            if(groups.Length==0)
            {
                Append(new MText{Contents="Chưa có tài nguyên được quản lý",Location=new Point3d(sectionX,y,0),TextHeight=220,Width=targetWidth,LayerId=labelLayer,TextStyleId=labelStyle},heading);y-=2000;
            }
            foreach(var group in groups)
            {
                double groupTarget=frameWidths.TryGetValue(kind+"|"+group.Key,out double widthOverride)?Math.Max(17000,widthOverride-1600):targetWidth;
                double groupTop=y,groupWidth=groupTarget;
                var titleItem=group.First();
                Append(new MText{Contents="Nhóm | "+Safe(group.Key==""?"Chưa phân nhóm":group.Key)+" ("+group.Count()+")",Location=new Point3d(sectionX,y,0),TextHeight=350,Width=targetWidth,LayerId=labelLayer,TextStyleId=labelStyle},heading);
                y-=1000;double cursorX=sectionX,rowTop=y,rowHeight=0;
                foreach(var item in group)
                {
                    var resource=Resolve(db,tx,item);var value=item.Values;Entity sample;
                    bool reused=preserved.TryGetValue(item.Id,out var previousSample);
                    if(reused)sample=previousSample!;
                    else if(resource is LayerTableRecord layer)sample=new Line(Point3d.Origin,new Point3d(5200,0,0)){LayerId=layer.ObjectId,ColorIndex=256,LinetypeScale=100};
                    else if(resource is BlockTableRecord block)sample=new BlockReference(Point3d.Origin,block.ObjectId);
                    else if(resource is TextStyleTableRecord text)sample=new DBText{TextString="Aa Bb 0123456789 – Mẫu chữ",TextStyleId=text.ObjectId,Height=text.TextSize>0?text.TextSize:200,Position=Point3d.Origin,LayerId=labelLayer};
                    else if(resource is DimStyleTableRecord dim)sample=new RotatedDimension(0,new Point3d(0,0,0),new Point3d(3000,0,0),new Point3d(0,800,0),"",dim.ObjectId){LayerId=labelLayer};
                    else sample=resource.OwnerId==model.ObjectId?(Hatch)resource:(Hatch)((Hatch)resource).Clone();
                    bool residentHatch=!sample.ObjectId.IsNull;
                    if(!residentHatch)Append(sample,item);
                    else{if(!sample.IsWriteEnabled)sample.UpgradeOpen();Tag(sample,tx,SampleKey,new LibrarySyncItem{Id=item.Id,Kind=item.Kind,Name=item.Name});}
                    if(sample is Dimension dimension)dimension.RecomputeDimensionBlock(true);
                    if(sample is Hatch displayHatch)displayHatch.Associative=false;
                    var attributePairs=new List<Tuple<AttributeDefinition,AttributeReference>>();
                    if(!reused&&sample is BlockReference measuredReference)
                    {
                        var definition=(BlockTableRecord)tx.GetObject(measuredReference.BlockTableRecord,OpenMode.ForRead);
                        foreach(ObjectId child in definition)
                        {
                            if(tx.GetObject(child,OpenMode.ForRead) is not AttributeDefinition attribute||attribute.Constant)continue;
                            var att=new AttributeReference();att.SetAttributeFromBlock(attribute,measuredReference.BlockTransform);att.TextString=attribute.TextString;
                            measuredReference.AttributeCollection.AppendAttribute(att);tx.AddNewlyCreatedDBObject(att,true);attributePairs.Add(Tuple.Create(attribute,att));
                        }
                    }
                    Extents3d bounds;
                    try{bounds=sample.GeometricExtents;}
                    catch(Autodesk.AutoCAD.Runtime.Exception error){throw new InvalidOperationException("Không tạo được mẫu "+item.Name+": "+error.Message);}
                    // Newly inserted annotative blocks report paper extents until the drawing is regenerated.
                    // Reserve their model-space envelope before saving, without changing the definition or annotation scale.
                    if(!reused&&sample is BlockReference annotativeReference&&((BlockTableRecord)tx.GetObject(annotativeReference.BlockTableRecord,OpenMode.ForRead)).Annotative==AnnotativeStates.True)
                    {
                        var annotation=db.Cannoscale;double factor=annotation.PaperUnits>0?annotation.DrawingUnits/annotation.PaperUnits:1;
                        if(factor>1)bounds.TransformBy(Matrix3d.Scaling(factor,annotativeReference.Position));
                    }
                    double sampleWidth=Math.Max(6000,bounds.MaxPoint.X-bounds.MinPoint.X+700),sampleHeight=Math.Max(1800,bounds.MaxPoint.Y-bounds.MinPoint.Y+700);
                    string details=item.Kind=="layers"?"ACI "+value["color"]+" | LW "+value["lineWeight"]+" | "+value["lineType"]+" | In "+value["plottable"]:
                        item.Kind=="hatches"?value["pattern"]+" | Scale "+value["scale"]+" | Góc "+Math.Round(value.Value<double>("angle")*180/Math.PI,2)+"° | ACI "+value["color"]:
                        item.Kind=="texts"?"Font "+value["font"]+" | Cao "+value["height"]+" | Rộng "+value["widthFactor"]:
                        item.Kind=="dims"?"Text "+value["textStyle"]+" | Scale "+value["scale"]+" | Chữ "+value["textHeight"]:"Đơn vị "+value["units"]+" | Block động "+value["dynamic"];
                    var label=new MText{Contents=Safe(item.Name)+"\\P"+Safe(details)+"\\PNhóm | "+Safe(item.Group)+"\\PCông dụng | "+Safe(item.Purpose)+"\\PGhi chú | "+Safe(item.Notes),Location=Point3d.Origin,Width=Math.Max(16000,sampleWidth),TextHeight=180,TextStyleId=labelStyle,LayerId=labelLayer};
                    Append(label,item);double labelSpace=Math.Max(1900,label.ActualHeight+500),width=Math.Max(17000,sampleWidth+600),height=sampleHeight+labelSpace+500;
                    if(cursorX>sectionX&&cursorX+width>sectionX+groupTarget){cursorX=sectionX;rowTop-=rowHeight+gap;rowHeight=0;}
                    label.Location=new Point3d(cursorX+250,rowTop-250,0);
                    var displacement=Matrix3d.Displacement(new Vector3d(cursorX+300-bounds.MinPoint.X,rowTop-labelSpace-bounds.MaxPoint.Y,0));
                    if(reused)LibraryFrames.Translate(sample,tx,new Vector3d(cursorX+300-bounds.MinPoint.X,rowTop-labelSpace-bounds.MaxPoint.Y,0));else sample.TransformBy(displacement);
                    if(sample is BlockReference reference)
                    {
                        foreach(var pair in attributePairs)
                        {
                            pair.Item2.SetAttributeFromBlock(pair.Item1,reference.BlockTransform);pair.Item2.TextString=pair.Item1.TextString;
                        }
                    }
                    var frame=new Polyline(4){Closed=true,LayerId=frameLayer};frame.AddVertexAt(0,new Point2d(cursorX,rowTop),0,0,0);frame.AddVertexAt(1,new Point2d(cursorX+width,rowTop),0,0,0);frame.AddVertexAt(2,new Point2d(cursorX+width,rowTop-height),0,0,0);frame.AddVertexAt(3,new Point2d(cursorX,rowTop-height),0,0,0);Append(frame,item);
                    groupWidth=Math.Max(groupWidth,cursorX-sectionX+width);cursorX+=width+gap;rowHeight=Math.Max(rowHeight,height);
                }
                y=rowTop-rowHeight-gap;sectionWidth=Math.Max(sectionWidth,groupWidth);
                View("SYNC_"+kind+"_"+JsonUtil.Hash(group.Key).Substring(0,8),sectionX,sectionX+groupWidth,groupTop,y,titleItem);y-=1400;
            }
            View("SYNC_"+(section+1).ToString("00")+"_"+kinds[section],sectionX,sectionX+sectionWidth,top,y,heading);
            layoutBottom=Math.Min(layoutBottom,y);
            sectionX+=sectionWidth+sectionGap;
        }
        var overview=new LibrarySyncItem{Id=JsonUtil.Hash("library-overview").Substring(0,32),Kind="blocks",Name="Thư viện CAD"};
        View("SYNC_00_ALL",layoutLeft,sectionX-sectionGap,top,layoutBottom,overview);
        LibraryFrames.Build(db,tx,items);
        LibraryFrames.Separate(db,tx,items);
        var overviewView=(ViewTableRecord)tx.GetObject(views["SYNC_00_ALL"],OpenMode.ForRead);
        var viewportTable=(ViewportTable)tx.GetObject(db.ViewportTableId,OpenMode.ForRead);
        foreach(ObjectId id in viewportTable)
        {
            var viewport=(ViewportTableRecord)tx.GetObject(id,OpenMode.ForWrite);if(viewport.Name!="*Active")continue;
            double aspect=viewport.Height>0&&viewport.Width>0?viewport.Width/viewport.Height:1.6;
            viewport.CenterPoint=overviewView.CenterPoint;viewport.Height=Math.Max(overviewView.Height,overviewView.Width/aspect);viewport.Width=viewport.Height*aspect;viewport.Target=Point3d.Origin;viewport.ViewDirection=Vector3d.ZAxis;
        }
    }

    static string Safe(string text) => text.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}").Replace("\r", " ").Replace("\n", " ");

    static void HatchPreviews(Database db, Transaction tx, IEnumerable<LibrarySyncItem> items)
    {
        var result = new JArray();
        foreach (var item in items)
        {
            try
            {
                var hatch = (Hatch)Resolve(db, tx, item);
                string file = item.Id + ".png", path = Path.Combine(Root, file);
                if (hatch.IsSolidFill)
                {
                    using var bitmap = new System.Drawing.Bitmap(240,180);
                    using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                    graphics.Clear(System.Drawing.Color.FromArgb(247,249,252)); graphics.FillRectangle(System.Drawing.Brushes.SlateGray,12,12,216,156);
                    bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
                }
                else
                {
                    var bounds = hatch.GeometricExtents;var lines = new System.Text.StringBuilder();
                    using var exploded = new DBObjectCollection();hatch.Explode(exploded);
                    int count=0;
                    string F(double value)=>value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
                    foreach(DBObject obj in exploded)
                    {
                        try{if(obj is Line l && count++<8000)lines.Append("<polyline points=\""+F(l.StartPoint.X)+","+F(-l.StartPoint.Y)+" "+F(l.EndPoint.X)+","+F(-l.EndPoint.Y)+"\"/>");}
                        finally{obj.Dispose();}
                    }
                    if(lines.Length==0)throw new InvalidOperationException("Mẫu hatch chưa có hình xem trước.");
                    BlockPreviews.Thumbnail("<svg>"+lines+"</svg>",path,bounds.MinPoint.X,bounds.MaxPoint.Y,bounds.MaxPoint.X-bounds.MinPoint.X,bounds.MaxPoint.Y-bounds.MinPoint.Y);
                }
                result.Add(new JObject{["id"]=item.Id,["file"]=file,["hash"]=JsonUtil.FileHash(path)});
            }
            catch(System.Exception error){result.Add(new JObject{["id"]=item.Id,["error"]=error.Message});}
        }
        JsonUtil.Atomic(Path.Combine(Root,"sync-previews.json"),result);
    }
}
