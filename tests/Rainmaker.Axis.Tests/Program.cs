using System;
using System.Linq;
using Rainmaker.Axis;

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
