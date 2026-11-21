using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Rainmaker.Axis;

internal static class AxisGridLayout
{
    // Spaces and semicolons separate bays. A decimal comma is accepted within a bay.
    internal static double[] ParseBays(string text)
    {
        var bays = new List<double>();
        foreach (string token in text.Split(new[] { ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split('*');
            int count = 1;
            if (parts.Length > 2 || (parts.Length == 2 &&
                (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1)))
                throw new ArgumentException("Nhập nhịp dạng 4000 5000 4000 hoặc 3*4000, đơn vị mm.");
            string expression=parts[parts.Length-1].Replace(',','.');double distance=0;
            if(expression.Length>512||!Regex.IsMatch(expression,@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[+-](?:\d+(?:\.\d*)?|\.\d+))*$"))
                throw new ArgumentException("Công thức nhịp không hợp lệ: "+token+". Ví dụ 3000+2000 hoặc 1000-500; không có dấu cách trong công thức.");
            foreach(Match term in Regex.Matches(expression,@"[+-]?(?:\d+(?:\.\d*)?|\.\d+)")){if(!double.TryParse(term.Value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value)||double.IsInfinity(value))throw new ArgumentException("Giá trị trong công thức nhịp quá lớn.");distance+=value;}
            if (double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0 || distance > 1000000)
                throw new ArgumentException("Khoảng cách nhịp phải lớn hơn 0 và tối đa 1.000.000 mm.");
            if (count > 99 - bays.Count)
                throw new ArgumentException("Mỗi phương tối đa 99 nhịp / 100 trục.");
            bays.AddRange(Enumerable.Repeat(distance, count));
        }
        if (bays.Count == 0) throw new ArgumentException("Cần ít nhất một nhịp cho mỗi phương.");
        return bays.ToArray();
    }

    internal static double[] Positions(double[] bays)
    {
        if (bays.Length < 1 || bays.Length > 99 || bays.Any(b => double.IsNaN(b) || double.IsInfinity(b) || b <= 0 || b > 1000000))
            throw new ArgumentException("Mỗi phương cần từ 1 đến 99 nhịp có khoảng cách hợp lệ.");
        var positions = new double[bays.Length + 1];
        for (int i = 0; i < bays.Length; i++) positions[i + 1] = positions[i] + bays[i];
        return positions;
    }

    internal static string[] Labels(string start, int count)
    {
        start = start.Trim().ToUpperInvariant();
        if (count < 1 || count > 100) throw new ArgumentException("Số lượng trục từ 1 đến 100.");
        if (long.TryParse(start, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number >= 1 && number <= 99999999 - count + 1)
            return Enumerable.Range(0, count).Select(i => (number + i).ToString(CultureInfo.InvariantCulture)).ToArray();
        if (start.Length < 1 || start.Length > 4 || start.Any(c => c < 'A' || c > 'Z'))
            throw new ArgumentException("Ký hiệu bắt đầu là số nguyên dương hoặc chữ A–Z (ví dụ 1, A, AA).");
        int ordinal = 0;
        foreach (char c in start) ordinal = ordinal * 26 + c - 'A' + 1;
        return Enumerable.Range(ordinal, count).Select(value =>
        {
            string label = "";
            while (value > 0)
            {
                value--;
                label = (char)('A' + value % 26) + label;
                value /= 26;
            }
            return label;
        }).ToArray();
    }
}
