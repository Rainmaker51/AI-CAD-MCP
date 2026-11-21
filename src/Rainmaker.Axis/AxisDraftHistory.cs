using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Rainmaker.Axis;
internal sealed class AxisDraftSnapshot
{
 public AxisGridParameters Parameters=new();
 public AxisLibrarySettings Settings=new();
 public string XText="",YText="",XLabelsText="",YLabelsText="";
 internal string Key=>JsonConvert.SerializeObject(this);
}
internal sealed partial class AxisGridDialog
{
 readonly List<AxisDraftSnapshot> undoDraft=new(),redoDraft=new();
 AxisDraftSnapshot? currentDraft;
 bool restoringDraft;
 Button undoButton=null!,redoButton=null!;
 void BuildHistoryButtons(Panel panel){undoButton=B("Hoàn tác",UndoDraft);redoButton=B("Làm lại",RedoDraft);undoButton.ToolTip="Ctrl+Z · hoàn tác bản nháp, không thay DWG";redoButton.ToolTip="Ctrl+Y · làm lại bản nháp";panel.Children.Add(undoButton);panel.Children.Add(redoButton);UpdateHistoryButtons();}
 void InitializeHistory(){PreviewKeyDown+=(_,e)=>{if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;if(e.Key==Key.Z){if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)RedoDraft();else UndoDraft();e.Handled=true;}else if(e.Key==Key.Y){RedoDraft();e.Handled=true;}};}
 void UpdateHistoryButtons(){if(undoButton==null)return;undoButton.IsEnabled=undoDraft.Count>0;redoButton.IsEnabled=redoDraft.Count>0;}
 void ResetHistory(){undoDraft.Clear();redoDraft.Clear();currentDraft=null;UpdateHistoryButtons();}
 void RecordHistory(AxisGridParameters p,AxisLibrarySettings s)
 {
  if(restoringDraft||loading||!ready)return;
  var copy=ConstructionDraft.Copy(p);copy.ShowColumns=copy.ShowWalls=copy.ShowHatch=true;
  var settingsCopy=JsonConvert.DeserializeObject<AxisLibrarySettings>(JsonConvert.SerializeObject(s))!;settingsCopy.Revision=0;
  var next=new AxisDraftSnapshot{Parameters=copy,Settings=settingsCopy,XText=xBays.Text,YText=yBays.Text,XLabelsText=xStart.Text,YLabelsText=yStart.Text};
  if(currentDraft!=null&&next.Key!=currentDraft.Key){undoDraft.Add(currentDraft);if(undoDraft.Count>100)undoDraft.RemoveAt(0);redoDraft.Clear();}currentDraft=next;UpdateHistoryButtons();
 }
 void CheckpointHistory(){if(!ready||loading||restoringDraft)return;try{RecordHistory(Read(),ReadSettings());}catch(ArgumentException){} }
 void UndoDraft(){timer.Stop();CheckpointHistory();if(undoDraft.Count==0){if(currentDraft!=null)RestoreDraft(currentDraft);return;}var next=undoDraft[undoDraft.Count-1];undoDraft.RemoveAt(undoDraft.Count-1);if(currentDraft!=null)redoDraft.Add(currentDraft);RestoreDraft(next);}
 void RedoDraft(){timer.Stop();CheckpointHistory();if(redoDraft.Count==0)return;var next=redoDraft[redoDraft.Count-1];redoDraft.RemoveAt(redoDraft.Count-1);if(currentDraft!=null)undoDraft.Add(currentDraft);RestoreDraft(next);}
 void RestoreDraft(AxisDraftSnapshot snapshot)
 {
  restoringDraft=true;loading=true;
  try{var p=ConstructionDraft.Copy(snapshot.Parameters);int revision=settings.Revision;settings=JsonConvert.DeserializeObject<AxisLibrarySettings>(JsonConvert.SerializeObject(snapshot.Settings))!;settings.Revision=revision;PopulateResourceSelection();
   LoadEnvelope(p.Envelope);xBays.Text=snapshot.XText;yBays.Text=snapshot.YText;xStart.Text=snapshot.XLabelsText;yStart.Text=snapshot.YLabelsText;singleLabel.Text=p.SingleLabel;scale.Text=p.Scale.ToString();gridName.Text=p.GridName;modes.SelectedIndex=p.Single?1:0;gridMode.IsChecked=!p.Single;singleMode.IsChecked=p.Single;
   defaultColumn=ColumnOptions.Default(p);defaultWall=WallOptions.Default(p);columnOverrides=p.ColumnOverrides.ToDictionary(x=>x.Key,x=>x.Value.Copy());wallOverrides=p.WallOverrides.ToDictionary(x=>x.Key,x=>x.Value.Copy());selectedNodes=p.ColumnNodes.ToArray();selectedSegments=p.WallSegments.ToArray();dirtyColumns.Clear();dirtyWalls.Clear();
   var nodes=new HashSet<string>(ConstructionGeometry.Nodes(p));var segments=new HashSet<string>(ConstructionGeometry.Segments(p));pickedNodes.IntersectWith(nodes);pickedSegments.IntersectWith(segments);columnsInitialized=p.Columns||selectedNodes.Length>0;wallsInitialized=p.Walls||selectedSegments.Length>0;
   columnsOn.IsChecked=p.Columns;wallsOn.IsChecked=p.Walls;hatchOn.IsChecked=p.WallHatch;fixedDim.IsChecked=p.FixedDimExtensions;dimExtension.Text=N(p.DimExtensionLength);LoadEditors();SyncPicked();currentDraft=snapshot;draftDirty=true;loadError="";
  }finally{loading=false;}
  try{UpdateState();}finally{restoringDraft=false;UpdateHistoryButtons();}
 }
}
