using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AICad.Contracts;

public sealed partial class CadToolCatalog
{
 public static string RibbonTarget(CadToolIndex index,CadToolEntry tool,CadRibbonPin pin)
 {
  string id=pin.TabId!=""?pin.TabId:tool.TabId;
  return index.Tabs.Any(t=>t.Id==id)?id:DefaultTabId;
 }
 public static string RibbonLayoutHash(CadToolEntry tool)=>Json.Hash(new{tool.TabId,pins=tool.RibbonPins.OrderBy(p=>p.Command,StringComparer.OrdinalIgnoreCase)});
 string RibbonHistory(string id)=>Path.Combine(root,"ribbon-history",Json.Name(id)+".json");
 public bool CanUndoRibbon(string id)
 {
  var tool=Read().Items.SingleOrDefault(t=>t.Id==id);string file=RibbonHistory(id);
  if(tool==null||!File.Exists(file))return false;
  var record=Json.Read<JObject>(File.ReadAllText(file));
  return record.Value<string>("package")==tool.PackageHash&&record.Value<string>("after")==RibbonLayoutHash(tool);
 }
 public void SetRibbonLayout(string id,int revision,string tabId,IEnumerable<CadRibbonPin> desired,string reason)=>Change(revision,index=>
 {
  var tool=Editable(index,id);RequireAddIn(tool);DemandTab(index,tabId);
  if(tool.DeletedUtc.HasValue)throw new CadException("TOOL_DISABLED","Khôi phục công cụ trước khi sửa Ribbon.");
  var pins=desired.Select(p=>Json.Read<CadRibbonPin>(Json.Write(p))).ToList();
  if(pins.Count>200||pins.Select(p=>p.Command).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=pins.Count)throw new CadException("TOOL_RIBBON","Danh sách nút bị trùng hoặc vượt giới hạn.");
  foreach(var pin in pins)
  {
   bool known=tool.Commands.Contains(pin.Command,StringComparer.OrdinalIgnoreCase);
   if(!known&&!tool.RibbonPins.Any(p=>p.Command==pin.Command))throw new CadException("TOOL_COMMAND","Lệnh không có trong danh sách đã lưu: "+pin.Command);
   ValidatePin(index,tool,pin,known);
  }
  var before=new{tabId=tool.TabId,pins=tool.RibbonPins};
  string previous=RibbonLayoutHash(tool);tool.TabId=tabId;tool.RibbonPins=pins;
  if(previous==RibbonLayoutHash(tool))return;
  Json.Atomic(RibbonHistory(id),new{schemaVersion=1,tool=id,package=tool.PackageHash,before,after=RibbonLayoutHash(tool),reason,utc=DateTime.UtcNow});
 });
 public void MoveRibbon(string id,int revision,string tabId)
 {
  var index=Read();if(index.Revision!=revision)throw new CadException("STALE_EDIT","Danh mục đã thay đổi. Làm mới rồi thử lại.");
  var tool=Editable(index,id);var target=index.Tabs.SingleOrDefault(t=>t.Id==tabId)??throw new CadException("RIBBON_TAB_MISSING","Tab đích không còn tồn tại.");
  if(!target.Enabled&&target.Id!=DefaultTabId)throw new CadException("RIBBON_TAB_HIDDEN","Tab đích đang ẩn. Bật tab ở trang Ribbon trước khi chuyển.");
  var pins=tool.RibbonPins.Select(p=>{var copy=Json.Read<CadRibbonPin>(Json.Write(p));copy.TabId="";return copy;});
  SetRibbonLayout(id,revision,tabId,pins,"MOVE_ALL");
 }
 public void UndoRibbon(string id,int revision)=>Change(revision,index=>
 {
  var tool=Editable(index,id);string file=RibbonHistory(id);
  if(!File.Exists(file))throw new CadException("RIBBON_UNDO","Không có thay đổi Ribbon để hoàn tác.");
  var record=Json.Read<JObject>(File.ReadAllText(file));
  if(record.Value<string>("package")!=tool.PackageHash||record.Value<string>("after")!=RibbonLayoutHash(tool))throw new CadException("RIBBON_UNDO_CHANGED","Cấu hình hoặc gói đã thay đổi ở nơi khác; không ghi đè bằng bản hoàn tác cũ.");
  string tab=record["before"]!.Value<string>("tabId")!;DemandTab(index,tab);
  var pins=record["before"]!["pins"]!.ToObject<List<CadRibbonPin>>()!;
  foreach(var pin in pins)ValidatePin(index,tool,pin,tool.Commands.Contains(pin.Command,StringComparer.OrdinalIgnoreCase));
  tool.TabId=tab;tool.RibbonPins=pins;
 });
}
