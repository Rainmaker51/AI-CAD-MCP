using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;

namespace AICad.Bridge;

internal static class CadToolRibbonSetup
{
 internal static string Location(CadToolIndex index,CadToolEntry tool)
 {
  var tabs=tool.RibbonPins.Where(p=>tool.Commands.Contains(p.Command,StringComparer.OrdinalIgnoreCase)).Select(p=>CadToolCatalog.RibbonTarget(index,tool,p)).Distinct().Select(id=>index.Tabs.Single(t=>t.Id==id)).Select(t=>t.Name+(!t.Enabled&&t.Id!=CadToolCatalog.DefaultTabId?" (ẩn)":"")).ToList();
  return tabs.Count==0?"Chưa ghim":string.Join("; ",tabs)+(tool.DeletedUtc.HasValue?" · thùng rác":!tool.Enabled?" · add-in tắt":"");
 }
 internal static string Status(CadToolIndex index,CadToolEntry tool)
 {
  var pins=tool.RibbonPins.Where(p=>tool.Commands.Contains(p.Command,StringComparer.OrdinalIgnoreCase)).ToList();
  int visible=pins.Count(p=>index.Tabs.Any(t=>t.Id==CadToolCatalog.RibbonTarget(index,tool,p)&&(t.Enabled||t.Id==CadToolCatalog.DefaultTabId)));
  if(tool.DeletedUtc.HasValue)return "Ribbon: công cụ trong thùng rác.";
  if(!tool.Enabled)return "Ribbon: công cụ đang tắt.";
  if(tool.Commands.Count==0)return "Ribbon: nạp DLL một lần để đọc lệnh, sau đó chọn ghim lên Ribbon.";
  if(pins.Count==0)return "Ribbon: chưa ghim nút nào · Chọn lệnh mở bảng hoặc đánh dấu trong danh sách.";
  return "Ribbon: "+pins.Count+"/"+tool.Commands.Count+" lệnh đã ghim · "+visible+" nút trên tab đang bật."+(visible<pins.Count?" Có nút nằm trên tab đang ẩn.":"");
 }

 // Saved discovery is sufficient for configuring UI; loading/running remains guarded by Runtime.
 internal static void PinAll(CadToolCatalog catalog,CadToolIndex index,CadToolEntry tool,string tabId,IEnumerable<CadToolCommand>? loaded)
 {
  if(tool.Commands.Count==0)throw new CadException("TOOL_COMMAND","Nạp DLL một lần để đọc danh sách lệnh trước khi ghim.");
  var suggestions=(loaded??Enumerable.Empty<CadToolCommand>()).ToList();
  catalog.PinCommands(tool.Id,index.Revision,tool.Commands.Select((name,i)=>
   suggestions.FirstOrDefault(c=>string.Equals(c.Name,name,StringComparison.OrdinalIgnoreCase))?.SuggestedPin
   ??new CadRibbonPin{Command=name,Label=name,Group="",Order=i*10}),tabId);
 }
}
