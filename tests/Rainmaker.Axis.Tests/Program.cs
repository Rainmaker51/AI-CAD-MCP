using System;
using System.Linq;
using Rainmaker.Axis;

var baseGrid=new[]{new AxisSegment(0,-100,0,3000),new AxisSegment(4000,0,4000,1500),new AxisSegment(4000,1500,4000,3000),new AxisSegment(-100,0,4100,0),new AxisSegment(0,3000,4000,3000)};
var recognized=AxisGridRecognition.Read(baseGrid);
if(!recognized.X.SequenceEqual(new double[]{0,4000})||!recognized.Y.SequenceEqual(new double[]{0,3000}))throw new Exception("Fragmented grid recognition failed");
void RecognitionReject(AxisSegment[] lines){try{AxisGridRecognition.Read(lines);}catch(ArgumentException){return;}throw new Exception("Invalid old grid accepted");}
RecognitionReject(baseGrid.Concat(new[]{new AxisSegment(0,0,100,100)}).ToArray());
RecognitionReject(new[]{baseGrid[0],new AxisSegment(4000,0,4000,1400),new AxisSegment(4000,1500,4000,3000),baseGrid[3],baseGrid[4]});
RecognitionReject(new[]{baseGrid[0],baseGrid[3]});
var near=AxisGridRecognition.Read(baseGrid.Concat(new[]{new AxisSegment(50,0,50,3000)}));
if(near.X.Length!=3||near.X[1]!=50)throw new Exception("Distinct 50 mm axes were merged");
var duplicateGrid=AxisGridRecognition.Read(baseGrid.Concat(baseGrid));
if(duplicateGrid.X.Length!=2||duplicateGrid.Y.Length!=2)throw new Exception("Duplicate segments created duplicate axes");
RecognitionReject(baseGrid.Concat(new[]{new AxisSegment(double.NaN,0,0,3000)}).ToArray());
RecognitionReject(baseGrid.Concat(new[]{new AxisSegment(8000,0,8000,3000)}).ToArray());
Console.WriteLine("PASS: joined/reversed/duplicate segments, distinct 50mm axes, disconnected and diagonal grid rejection");

void Check(bool condition)
{
    if (!condition) throw new Exception("Axis grid regression failed.");
}
void Reject(Action action)
{
    try { action(); }
    catch (ArgumentException) { return; }
    throw new Exception("Invalid axis grid input accepted.");
}
Check(AxisGridLayout.ParseBays("4000 5000;2*3500").SequenceEqual(new double[] { 4000, 5000, 3500, 3500 }));
Check(AxisGridLayout.ParseBays("1250,5 2000.25").SequenceEqual(new[] { 1250.5, 2000.25 }));
Check(AxisGridLayout.ParseBays("5000 3000+2000 1000-500 2000").SequenceEqual(new double[]{5000,5000,500,2000}));
Check(AxisGridLayout.ParseBays("2*3000+2000 1000-2000+3000 1250,5-0,5").SequenceEqual(new double[]{5000,5000,2000,1250}));
foreach(string invalid in new[]{"1000-1000","500-1000","2+","1--2","(1000+500)","2/2","1e3+1","1+NaN","1000000+1","1;99*1"})Reject(()=>AxisGridLayout.ParseBays(invalid));
Reject(()=>AxisGridLayout.ParseBays(new string('9',400)));
Check(AxisGridLayout.Positions(new double[] { 4000, 5000, 3500 }).SequenceEqual(new double[] { 0, 4000, 9000, 12500 }));
Check(AxisGridLayout.Labels("8", 4).SequenceEqual(new[] { "8", "9", "10", "11" }));
Check(AxisGridLayout.Labels("y", 4).SequenceEqual(new[] { "Y", "Z", "AA", "AB" }));
Check(AxisGridLayout.Labels("AZ", 3).SequenceEqual(new[] { "AZ", "BA", "BB" }));
Check(AxisGridLayout.ParseBays("99*4000").Length == 99);
foreach (string invalid in new[] { "", "0", "-1", "NaN", "Infinity", "1e9", "3**4000", "0*4000", "100*1", "9999999999*1", "4000;1000001" })
    Reject(() => AxisGridLayout.ParseBays(invalid));
foreach (string invalid in new[] { "", "0", "-1", "A1", "99999999", "ABCDE" })
    Reject(() => AxisGridLayout.Labels(invalid, 3));
Reject(() => AxisGridLayout.Positions(new[] { double.NaN }));
Reject(() => AxisGridLayout.Positions(Array.Empty<double>()));
Console.WriteLine("PASS: grid bays, repeated bays, decimal input, cumulative positions, numeric/alphabetic labels and invalid inputs.");
var envelope=new AxisEnvelope();envelope.Validate();
Check(envelope.SpanX==5780&&envelope.SpanY==14780);
Check(envelope.RemainingY(new double[]{5000,5000})==4780);
Check(envelope.RemainingY(new double[]{5000,5000,4780})==0);
Check(envelope.RemainingY(new double[]{5000,5000,5000})==-220);
Check(envelope.RemainingX(Array.Empty<double>())==5780);
foreach(int count in new[]{1,3,7,99}){var divided=AxisEnvelope.Divide(envelope.SpanY,count);Check(divided.Length==count&&envelope.RemainingY(divided)==0&&divided.Max()-divided.Min()<1e-7);}
Reject(()=>AxisEnvelope.Divide(100,0));Reject(()=>AxisEnvelope.Divide(100,100));Reject(()=>new AxisEnvelope{Width=200}.Validate());Reject(()=>new AxisEnvelope{Left=-1}.Validate());Reject(()=>new AxisEnvelope{Width=double.NaN}.Validate());
Reject(()=>envelope.DemandMatch(new double[]{5780},new double[]{5000,5000}));envelope.DemandMatch(new double[]{5780},new double[]{5000,5000,4780});
Console.WriteLine("PASS: envelope spans, final remainder, no duplicate deduction, overflow, equal bays and invalid profiles.");
