namespace RugsManagment.Application.Features.Import;
public static class KnownValueSplitter
{
    public static bool Apply(SheetGrid sheet, int row, IReadOnlyDictionary<string, int> mapping, Dictionary<string, string?> values)
    {
        var changed = false;
        foreach (var merge in sheet.Merges.Where(m => m.FirstRow == row && m.LastColumn > m.FirstColumn))
        {
            if (!mapping.TryGetValue("origin", out var origin) || origin < merge.FirstColumn || origin > merge.LastColumn) continue;
            if (mapping.TryGetValue("serial", out var serial) && serial == merge.FirstColumn) continue;
            var text = sheet.Text(row, merge.FirstColumn);
            foreach (var material in new[] { "پشم", "ابریشم", "پنبه" })
            {
                if (!text.Contains(material, StringComparison.Ordinal)) continue;
                values["material"] = material;
                values["origin"] = text.Replace(material, "", StringComparison.Ordinal).Replace("/", "", StringComparison.Ordinal).Trim();
                if (mapping.TryGetValue("pattern", out var pattern) && pattern >= merge.FirstColumn && pattern <= merge.LastColumn) values["pattern"] = null;
                changed = true;
                break;
            }
        }
        return changed;
    }
}
