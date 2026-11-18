using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;

public sealed class LibraryFrameNode
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string Parent { get; set; } = "";
    public bool Manual { get; set; }
    public double RequestedWidth { get; set; }
    public double RequestedHeight { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double DrawnWidth { get; set; }
    public double DrawnHeight { get; set; }
    public string Label => LibraryFrames.KindName(Kind)+(Path==""?"":" / "+Path);
    public override string ToString()=>Label;
}

/// <summary>Frame blocks contain borders/attributes only. Samples stay editable in Model Space.</summary>
public static class LibraryFrames
{
    public const string FrameKey="AICAD_LIB_FRAME";
    const string Registry="AICAD_LIB_MANAGER";
    public static string KindName(string kind)=>kind switch{"blocks"=>"Block","layers"=>"Layer","hatches"=>"Hatch","texts"=>"Text","dims"=>"Dim",_=>kind};
    public static bool Under(string path,string parent)=>parent==""||path==parent||path.StartsWith(parent+" / ",StringComparison.Ordinal);
    static JObject? ReadRecord(DBObject owner,Transaction tx,string key)
    {
        if(owner.ExtensionDictionary.IsNull)return null;var dic=(DBDictionary)tx.GetObject(owner.ExtensionDictionary,OpenMode.ForRead);
        if(!dic.Contains(key))return null;var record=(Xrecord)tx.GetObject(dic.GetAt(key),OpenMode.ForRead);
        return AICad.Contracts.Json.Read<JObject>(string.Concat(record.Data.AsArray().Select(v=>v.Value.ToString())));
    }
    static void Record(DBObject owner,Transaction tx,string key,object value)
    {
        if(!owner.IsWriteEnabled)owner.UpgradeOpen();if(owner.ExtensionDictionary.IsNull)owner.CreateExtensionDictionary();
        var dic=(DBDictionary)tx.GetObject(owner.ExtensionDictionary,OpenMode.ForWrite);Xrecord record;
        if(dic.Contains(key))record=(Xrecord)tx.GetObject(dic.GetAt(key),OpenMode.ForWrite);else{record=new Xrecord();dic.SetAt(key,record);tx.AddNewlyCreatedDBObject(record,true);}
        string text=AICad.Contracts.Json.Write(value);record.Data=new ResultBuffer(Enumerable.Range(0,(text.Length+199)/200).Select(i=>new TypedValue((int)DxfCode.Text,text.Substring(i*200,Math.Min(200,text.Length-i*200)))).ToArray());
    }
    public static bool Initialized(Database db,Transaction tx)=>ReadRecord(tx.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead),tx,Registry)!=null;
    public static List<LibraryFrameNode> Nodes(Database db,Transaction tx)
    {
        var data=ReadRecord(tx.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead),tx,Registry);return data?["nodes"]?.ToObject<List<LibraryFrameNode>>()??new();
    }
    public static void Save(Database db,Transaction tx,List<LibraryFrameNode> nodes)=>Record(tx.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite),tx,Registry,new{version=1,nodes});
    public static LibraryFrameNode? Info(DBObject obj,Transaction tx)=>ReadRecord(obj,tx,FrameKey)?.ToObject<LibraryFrameNode>();
    public static IEnumerable<Entity> Model(Database db,Transaction tx)
    {
        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);return ((BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead)).Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Entity>();
    }
    public static Dictionary<string,double> Widths(Database db,Transaction tx)
    {
        var result=new Dictionary<string,double>();foreach(var node in Nodes(db,tx))if(node.RequestedWidth>0)result[node.Kind+"|"+node.Path]=node.RequestedWidth;
        foreach(var frame in Model(db,tx).OfType<BlockReference>())if(Info(frame,tx) is LibraryFrameNode node&&(node.RequestedWidth>0||Math.Abs(frame.ScaleFactors.X-node.DrawnWidth)>.01))result[node.Kind+"|"+node.Path]=Math.Abs(frame.ScaleFactors.X);
        return result;
    }
    public static void Clear(Database db,Transaction tx)
    {
        var nodes=Nodes(db,tx);foreach(var frame in Model(db,tx).OfType<BlockReference>().ToArray())
        {
            var info=Info(frame,tx);if(info==null)continue;var node=nodes.FirstOrDefault(n=>n.Id==info.Id);
            if(node!=null){node.X=frame.Position.X;node.Y=frame.Position.Y;if(info.RequestedWidth>0||Math.Abs(frame.ScaleFactors.X-info.DrawnWidth)>.01||Math.Abs(frame.ScaleFactors.Y-info.DrawnHeight)>.01){node.RequestedWidth=Math.Abs(frame.ScaleFactors.X);node.RequestedHeight=Math.Abs(frame.ScaleFactors.Y);}}
            frame.UpgradeOpen();frame.Erase();
        }
        if(nodes.Count>0)Save(db,tx,nodes);
        var views=(ViewTable)tx.GetObject(db.ViewTableId,OpenMode.ForRead);foreach(ObjectId id in views.Cast<ObjectId>().ToArray()){var view=(ViewTableRecord)tx.GetObject(id,OpenMode.ForRead);if(Info(view,tx)==null)continue;view.UpgradeOpen();view.Erase();}
    }
    public static LibraryFrameNode Ensure(List<LibraryFrameNode> nodes,string kind,string path)
    {
        if(!LibrarySyncRules.Kinds.Contains(kind))throw new CadException("LIBRARY_KIND","Loại thư viện không hợp lệ.");
        path=string.Join(" / ",path.Split('/').Select(s=>s.Trim()).Where(s=>s!=""));
        if(path.Length>120||path.Split('/').Length>15||path.Any(char.IsControl))throw new CadException("LIBRARY_GROUP","Đường dẫn nhóm tối đa 120 ký tự, 15 cấp.");
        var existing=nodes.FirstOrDefault(n=>n.Kind==kind&&n.Path==path);if(existing!=null)return existing;
        if(nodes.Count>=1000)throw new CadException("LIBRARY_GROUP_LIMIT","Một DWG hỗ trợ tối đa 1.000 nhóm quản lý.");
        string parent=path.Contains(" / ")?path.Substring(0,path.LastIndexOf(" / ",StringComparison.Ordinal)):"";
        if(path!="")Ensure(nodes,kind,parent);
        var created=new LibraryFrameNode{Id=Guid.NewGuid().ToString("N"),Kind=kind,Path=path,Parent=parent};nodes.Add(created);return created;
    }
    static ObjectId Definition(Database db,Transaction tx)
    {
        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);const string name="AICAD_LIB_FRAME_V1";if(bt.Has(name))return bt[name];
        var definition=new BlockTableRecord{Name=name,Units=UnitsValue.Millimeters};bt.Add(definition);tx.AddNewlyCreatedDBObject(definition,true);
        var border=new Polyline(4){Closed=true,ColorIndex=0};border.AddVertexAt(0,new Point2d(0,0),0,0,0);border.AddVertexAt(1,new Point2d(1,0),0,0,0);border.AddVertexAt(2,new Point2d(1,-1),0,0,0);border.AddVertexAt(3,new Point2d(0,-1),0,0,0);definition.AppendEntity(border);tx.AddNewlyCreatedDBObject(border,true);
        foreach(string tag in new[]{"TITLE","PATH","COUNT","GROUP_ID","KIND"})
        {
            var att=new AttributeDefinition{Tag=tag,Prompt=tag,TextString=tag,Height=1,Invisible=tag=="GROUP_ID"||tag=="KIND"};definition.AppendEntity(att);tx.AddNewlyCreatedDBObject(att,true);
        }
        return definition.ObjectId;
    }
    public static void Resize(BlockReference frame,Transaction tx,double width,double height)
    {
        if(double.IsNaN(width)||double.IsInfinity(width)||double.IsNaN(height)||double.IsInfinity(height)||width<2000||height<1500||width>2000000||height>2000000)throw new CadException("LIBRARY_SIZE","Khung rộng từ 2.000, cao từ 1.500 và tối đa 2.000.000 mm.");
        if(!frame.IsWriteEnabled)frame.UpgradeOpen();frame.ScaleFactors=new Scale3d(width,height,1);PlaceAttributes(frame,tx);
        var info=Info(frame,tx)!;info.RequestedWidth=width;info.RequestedHeight=height;Record(frame,tx,FrameKey,info);
        var nodes=Nodes(frame.Database,tx);var node=nodes.FirstOrDefault(n=>n.Id==info.Id);if(node!=null){node.RequestedWidth=width;node.RequestedHeight=height;Save(frame.Database,tx,nodes);}
    }
    public static void PlaceAttributes(BlockReference frame,Transaction tx)
    {
        int line=0;foreach(ObjectId id in frame.AttributeCollection)
        {
            var attribute=(AttributeReference)tx.GetObject(id,OpenMode.ForWrite);attribute.Height=attribute.Tag=="TITLE"?320:190;attribute.WidthFactor=1;attribute.Rotation=0;
            attribute.Position=frame.Position+new Vector3d(350,-450-line*350,0);if(!attribute.Invisible)line++;
        }
    }
    static Extents3d Union(IEnumerable<Extents3d> boxes)
    {
        var array=boxes.ToArray();var result=array[0];foreach(var box in array.Skip(1))result.AddExtents(box);return result;
    }
    public static void Build(Database db,Transaction tx,List<LibrarySyncItem> items)
    {
        var nodes=Nodes(db,tx);foreach(string kind in LibrarySyncRules.Kinds)Ensure(nodes,kind,"");foreach(var item in items)Ensure(nodes,item.Kind,item.Group);
        var cells=Model(db,tx).OfType<Polyline>().Where(e=>e.Closed&&e.NumberOfVertices==4&&LibrarySyncNative.SampleMetadata(e,tx)!=null).Select(e=>new{item=items.FirstOrDefault(i=>i.Id==LibrarySyncNative.SampleMetadata(e,tx)!.Id),box=e.GeometricExtents}).Where(e=>e.item!=null).ToArray();
        var boxes=new Dictionary<string,Extents3d>();var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
        var views=(ViewTable)tx.GetObject(db.ViewTableId,OpenMode.ForRead);var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead);ObjectId layer=layers.Has("QA_KH_Khung Ten")?layers["QA_KH_Khung Ten"]:layers["0"];
        var styles=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForRead);ObjectId style=styles.Has("QA_Tahoma")?styles["QA_Tahoma"]:db.Textstyle;
        int empty=0;
        foreach(var node in nodes.OrderByDescending(n=>n.Path.Split('/').Length).ThenByDescending(n=>n.Path.Length))
        {
            var contents=cells.Where(c=>c.item!.Kind==node.Kind&&c.item.Group==node.Path).Select(c=>c.box).Concat(nodes.Where(n=>n.Kind==node.Kind&&n.Parent==node.Path&&n.Path!=node.Path&&boxes.ContainsKey(n.Id)).Select(n=>boxes[n.Id])).ToArray();
            Extents3d box;
            if(contents.Length>0)box=Union(contents);
            else
            {
                int index=Array.IndexOf(LibrarySyncRules.Kinds,node.Kind);string viewName="SYNC_"+(index+1).ToString("00")+"_"+KindName(node.Kind).ToUpperInvariant();
                Point2d origin=views.Has(viewName)?((ViewTableRecord)tx.GetObject(views[viewName],OpenMode.ForRead)).CenterPoint:new Point2d(100000+index*65000,0);
                double y=origin.Y-10000-empty++*6000;double x=origin.X;if(node.X.HasValue&&node.Y.HasValue){x=node.X.Value+700;y=node.Y.Value-1550;}box=new Extents3d(new Point3d(x,y-2500,0),new Point3d(x+17000,y,0));
            }
            double left=box.MinPoint.X-700,upper=box.MaxPoint.Y+1550,width=box.MaxPoint.X-left+700,height=upper-box.MinPoint.Y+700;
            width=Math.Max(width,node.RequestedWidth);height=Math.Max(height,node.RequestedHeight);
            node.DrawnWidth=width;node.DrawnHeight=height;
            var reference=new BlockReference(new Point3d(left,upper,0),Definition(db,tx)){LayerId=layer,ColorIndex=node.Path==""?150:8,ScaleFactors=new Scale3d(width,height,1)};model.AppendEntity(reference);tx.AddNewlyCreatedDBObject(reference,true);Record(reference,tx,FrameKey,node);
            var definition=(BlockTableRecord)tx.GetObject(reference.BlockTableRecord,OpenMode.ForRead);
            foreach(ObjectId id in definition)
            {
                if(tx.GetObject(id,OpenMode.ForRead) is not AttributeDefinition ad)continue;
                string text=ad.Tag switch{"TITLE"=>node.Path==""?KindName(node.Kind).ToUpperInvariant():node.Path.Split('/').Last().Trim(),"PATH"=>node.Label,"COUNT"=>items.Count(i=>i.Kind==node.Kind&&Under(i.Group,node.Path))+" mẫu","GROUP_ID"=>node.Id,"KIND"=>node.Kind,_=>""};
                var att=new AttributeReference{Tag=ad.Tag,TextString=text,Invisible=ad.Invisible,TextStyleId=style,Height=200};reference.AttributeCollection.AppendAttribute(att);tx.AddNewlyCreatedDBObject(att,true);
            }
            PlaceAttributes(reference,tx);boxes[node.Id]=new Extents3d(new Point3d(left,upper-height,0),new Point3d(left+width,upper,0));
        }
        // Frame attributes replace generated section/group headings. Item descriptions stay editable.
        var itemIds=items.Select(i=>i.Id).ToHashSet();foreach(var text in Model(db,tx).OfType<MText>().ToArray())if(LibrarySyncNative.SampleMetadata(text,tx) is LibrarySyncItem tag&&!itemIds.Contains(tag.Id)){text.UpgradeOpen();text.Erase();}
        void SetView(string name,Extents3d extent,LibraryFrameNode node)
        {
            ViewTableRecord view;if(views.Has(name))view=(ViewTableRecord)tx.GetObject(views[name],OpenMode.ForWrite);else{if(!views.IsWriteEnabled)views.UpgradeOpen();view=new ViewTableRecord{Name=name};views.Add(view);tx.AddNewlyCreatedDBObject(view,true);}
            view.CenterPoint=new Point2d((extent.MinPoint.X+extent.MaxPoint.X)/2,(extent.MinPoint.Y+extent.MaxPoint.Y)/2);view.Width=extent.MaxPoint.X-extent.MinPoint.X+2000;view.Height=extent.MaxPoint.Y-extent.MinPoint.Y+2000;view.ViewDirection=Vector3d.ZAxis;view.Target=Point3d.Origin;Record(view,tx,FrameKey,node);
        }
        foreach(var node in nodes)
        {
            string name=node.Path==""?"SYNC_"+(Array.IndexOf(LibrarySyncRules.Kinds,node.Kind)+1).ToString("00")+"_"+KindName(node.Kind).ToUpperInvariant():"SYNC_"+node.Kind+"_"+AICad.Contracts.Json.Hash(node.Path).Substring(0,8);SetView(name,boxes[node.Id],node);
        }
        SetView("SYNC_00_ALL",Union(nodes.Where(n=>n.Path=="").Select(n=>boxes[n.Id])),new LibraryFrameNode{Id="overview",Kind="blocks"});
        Save(db,tx,nodes);
    }
    public static List<LibrarySyncItem> ManagedItems(Database db,Transaction tx)=>LibrarySyncNative.ReadDrawing(db,tx).Where(i=>LibrarySyncNative.Managed(db,tx,i)).ToList();
    public static void Translate(Entity entity,Transaction tx,Vector3d offset)
    {
        var attributes=new List<Tuple<AttributeReference,Point3d,Point3d>>();
        if(entity is BlockReference block)foreach(ObjectId id in block.AttributeCollection){var att=(AttributeReference)tx.GetObject(id,OpenMode.ForWrite);attributes.Add(Tuple.Create(att,att.Position,att.AlignmentPoint));}
        if(!entity.IsWriteEnabled)entity.UpgradeOpen();entity.TransformBy(Matrix3d.Displacement(offset));
        // TransformBy may also transform owned attributes. Set their target positions from the original snapshot exactly once.
        foreach(var value in attributes){value.Item1.Position=value.Item2+offset;if(!value.Item1.IsDefaultAlignment)value.Item1.AlignmentPoint=value.Item3+offset;}
    }
    public static void MoveGroup(Database db,Transaction tx,LibraryFrameNode group,Vector3d offset,List<LibrarySyncItem> items)
    {
        var ids=items.Where(i=>i.Kind==group.Kind&&Under(i.Group,group.Path)).Select(i=>i.Id).ToHashSet();var transform=Matrix3d.Displacement(offset);
        foreach(var entity in Model(db,tx).ToArray())
        {
            var frame=Info(entity,tx);var sample=LibrarySyncNative.SampleMetadata(entity,tx);
            if(!(frame!=null&&frame.Kind==group.Kind&&Under(frame.Path,group.Path))&&!(sample!=null&&ids.Contains(sample.Id)))continue;
            Translate(entity,tx,offset);
        }
    }
    public static void PackGroup(Database db,Transaction tx,LibraryFrameNode group,List<LibrarySyncItem> items)
    {
        if(Nodes(db,tx).Any(n=>n.Kind==group.Kind&&Under(n.Path,group.Path)&&n.Manual))throw new CadException("LIBRARY_MANUAL","Nhóm hoặc nhóm con đang giữ bố trí thủ công. Tắt Giữ bố trí trước khi xếp gọn.");
        var frame=Model(db,tx).OfType<BlockReference>().FirstOrDefault(b=>Info(b,tx)?.Id==group.Id)??throw new CadException("LIBRARY_FRAME","Chưa có khung; khởi tạo bố trí trước.");
        double left=frame.Position.X+800,top=frame.Position.Y-1600,x=left,y=top,rowHeight=0,available=Math.Max(17000,Math.Abs(frame.ScaleFactors.X)-1600);
        var entities=Model(db,tx).ToArray();
        string? lastPath=null;
        foreach(var item in items.Where(i=>i.Kind==group.Kind&&Under(i.Group,group.Path)).OrderBy(i=>i.Group).ThenBy(i=>i.Name))
        {
            if(lastPath!=null&&lastPath!=item.Group){x=left;y-=rowHeight+3500;rowHeight=0;}lastPath=item.Group;
            var glyph=entities.Where(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==item.Id).ToArray();var cell=glyph.OfType<Polyline>().FirstOrDefault(e=>e.Closed&&e.NumberOfVertices==4);if(cell==null)continue;
            var bounds=cell.GeometricExtents;double width=bounds.MaxPoint.X-bounds.MinPoint.X,height=bounds.MaxPoint.Y-bounds.MinPoint.Y;
            if(x>left&&x+width>left+available){x=left;y-=rowHeight+450;rowHeight=0;}
            var transform=Matrix3d.Displacement(new Vector3d(x-bounds.MinPoint.X,y-bounds.MaxPoint.Y,0));
            foreach(var entity in glyph)Translate(entity,tx,new Vector3d(x-bounds.MinPoint.X,y-bounds.MaxPoint.Y,0));
            x+=width+450;rowHeight=Math.Max(rowHeight,height);
        }
        Clear(db,tx);Build(db,tx,items);
    }
    public static void AddCell(Database db,Transaction tx,LibrarySyncItem item,Entity sample)
    {
        if(Model(db,tx).OfType<Polyline>().Any(e=>e.Closed&&LibrarySyncNative.SampleMetadata(e,tx)?.Id==item.Id))return;
        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var ex=sample.GeometricExtents;
        double left=ex.MinPoint.X-300,upper=ex.MaxPoint.Y+1900,width=Math.Max(17000,ex.MaxPoint.X-left+700),height=upper-ex.MinPoint.Y+700;
        var frame=new Polyline(4){Closed=true};frame.AddVertexAt(0,new Point2d(left,upper),0,0,0);frame.AddVertexAt(1,new Point2d(left+width,upper),0,0,0);frame.AddVertexAt(2,new Point2d(left+width,upper-height),0,0,0);frame.AddVertexAt(3,new Point2d(left,upper-height),0,0,0);ms.AppendEntity(frame);tx.AddNewlyCreatedDBObject(frame,true);LibrarySyncNative.MarkSample(frame,tx,item);
        var label=new MText{Contents=item.Name.Replace("\\","\\\\").Replace("{","\\{").Replace("}","\\}")+"\\PNhóm | "+item.Group+"\\PCông dụng | "+item.Purpose+"\\PGhi chú | "+item.Notes,Location=new Point3d(left+250,upper-250,0),Width=width-500,TextHeight=180};ms.AppendEntity(label);tx.AddNewlyCreatedDBObject(label,true);LibrarySyncNative.MarkSample(label,tx,item);
    }
    public static void Separate(Database db,Transaction tx,List<LibrarySyncItem> items)
    {
        for(int attempt=0;attempt<Math.Max(10,Nodes(db,tx).Count*4);attempt++)
        {
            var frames=Model(db,tx).OfType<BlockReference>().Select(b=>new{reference=b,node=Info(b,tx)}).Where(x=>x.node!=null).ToArray();bool moved=false;
            for(int i=0;i<frames.Length&&!moved;i++)for(int j=i+1;j<frames.Length&&!moved;j++)
            {
                var a=frames[i];var b=frames[j];if(a.node!.Kind==b.node!.Kind&&(Under(a.node.Path,b.node.Path)||Under(b.node.Path,a.node.Path)))continue;
                double al=a.reference.Position.X,ar=al+a.reference.ScaleFactors.X,at=a.reference.Position.Y,ab=at-a.reference.ScaleFactors.Y;
                double bl=b.reference.Position.X,br=bl+b.reference.ScaleFactors.X,bt=b.reference.Position.Y,bb=bt-b.reference.ScaleFactors.Y;
                if(Math.Min(ar,br)-Math.Max(al,bl)<.01||Math.Min(at,bt)-Math.Max(ab,bb)<.01)continue;
                var target=b.node;Vector3d shift;
                if(a.node.Kind!=target.Kind){target=Nodes(db,tx).Single(n=>n.Kind==target.Kind&&n.Path=="");shift=new Vector3d(ar-bl+6000,0,0);}else shift=new Vector3d(0,ab-bt-3500,0);
                MoveGroup(db,tx,target,shift,items);Clear(db,tx);Build(db,tx,items);moved=true;
            }
            if(!moved)return;
        }
        throw new CadException("LIBRARY_LAYOUT","Không còn chỗ an toàn để tách các nhóm. Thử xếp gọn từng nhóm hoặc giảm chiều rộng khung.");
    }
}
