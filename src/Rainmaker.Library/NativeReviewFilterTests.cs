using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
namespace Rainmaker.Library;
public sealed partial class NativeTests
{
    static void VerifyReviewFilters(string evidence)
    {
        var differences=new JArray(LibrarySyncRules.Kinds.Select((k,n)=>new JObject{["Id"]=Guid.NewGuid().ToString("N"),["Status"]=n==2?"CAD_OUT_OF_SCOPE":"CAD_NEW",["Code"]="TEST-"+n,["Cad"]=new JObject{["Kind"]=k,["Name"]=k+" sample",["Group"]="Parent / Child"}}));
        differences[2]!["App"]=differences[2]!["Cad"]!.DeepClone();
        var review=new QaCadReview(new JObject{["differences"]=differences},new());
        try
        {
            foreach(var r in review.Rows)r.Selected=false;
            var kept=review.Rows.Single(r=>r.Kind=="layers");kept.Selected=true;
            review.KindFilter.SelectedItem=review.KindFilter.Items.Cast<QaCadReview.Pick>().Single(p=>p.Key=="blocks");
            Check(review.Filtered.Cast<QaCadReview.Row>().All(r=>r.Kind=="blocks")&&kept.Selected,"Kind filter cleared hidden selection");
            review.SelectionFilter.SelectedIndex=1;Check(review.Filtered.Count==0&&kept.Selected,"Combined selection filter failed");
            review.KindFilter.SelectedIndex=0;Check(review.Filtered.Count==1,"Selected-only filter did not show retained row");
            kept.Selected=false;var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);Check(review.Filtered.Count==0,"Selection filter did not update after checkbox change");
            review.SelectionFilter.SelectedIndex=0;review.StatusFilter.SelectedItem=review.StatusFilter.Items.Cast<QaCadReview.Pick>().Single(p=>p.Key=="CAD_OUT_OF_SCOPE");Check(review.Filtered.Count==1,"Status filter failed");
            Check(review.Rows.Single(r=>r.OutOfScope).Caption=="Ngoài khung quét · giữ danh mục","Vietnamese outside-frame caption is corrupted");
            Check(new QaCadReview.Pick{Text="Giữ ID hiện tại"}.ToString()=="Giữ ID hiện tại","Choice shows CLR type name");
            review.StatusFilter.SelectedIndex=0;review.SearchFilter.Text="TEST-1";Check(review.Filtered.Count==1&&review.Filtered.Cast<QaCadReview.Row>().Single().Kind=="layers","Resource-code search failed");
            review.SearchFilter.Clear();review.Rows[0].Selected=true;
            var renderFrame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>renderFrame.Continue=false));Dispatcher.PushFrame(renderFrame);
            var content=(FrameworkElement)review.Content;content.Measure(new Size(1408,800));content.Arrange(new Rect(0,0,1408,800));content.UpdateLayout();
            var bitmap=new RenderTargetBitmap(1408,800,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(evidence,"review-filters.png")))png.Save(file);
            foreach(var row in review.Rows)row.Selected=false;review.Rows.Single(r=>r.OutOfScope).Selected=true;
            Check(review.BuildPlan()["rows"]!.Single().Value<string>("action")=="exclude","Outside hatch cannot be explicitly excluded from catalog");
            AICad.Contracts.Json.Atomic(Path.Combine(evidence,"review-filters.json"),new{passed=true,kindAndStatus=true,selectedAndUnselected=true,hiddenSelectionPreserved=true,checkboxRefresh=true,resourceCodeSearch=true,vietnameseCaption=true,choiceLabels=true,hatchExclusion=true});
        }
        finally{review.Close();}
    }
}
