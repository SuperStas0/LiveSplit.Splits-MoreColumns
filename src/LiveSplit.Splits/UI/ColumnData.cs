using System;
using System.Xml;

namespace LiveSplit.UI;

public class ColumnData
{
    public string Name { get; set; }
    public ColumnType Type { get; set; }
    public string Comparison { get; set; }
    public string TimingMethod { get; set; }
    public bool ColorByRate { get; set; }

    public bool IsStatistic => Type is ColumnType.Completed or ColumnType.Reached or ColumnType.Passrate or ColumnType.Reachrate or ColumnType.EstimatedReachingTime;

    public ColumnData(string name, ColumnType type, string comparison, string method, bool colorByRate = false)
    {
        Name = name;
        Type = type;
        Comparison = comparison;
        TimingMethod = method;
        ColorByRate = colorByRate;
    }

    public static ColumnData FromXml(XmlNode node)
    {
        var element = (XmlElement)node;
        return new ColumnData(element["Name"].InnerText,
            (ColumnType)Enum.Parse(typeof(ColumnType), element["Type"].InnerText),
            element["Comparison"].InnerText,
            element["TimingMethod"].InnerText,
            SettingsHelper.ParseBool(element["ColorByRate"], false));
    }

    public int CreateElement(XmlDocument document, XmlElement element)
    {
        return SettingsHelper.CreateSetting(document, element, "Version", "1.5") ^
        SettingsHelper.CreateSetting(document, element, "Name", Name) ^
        SettingsHelper.CreateSetting(document, element, "Type", Type) ^
        SettingsHelper.CreateSetting(document, element, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, element, "TimingMethod", TimingMethod) ^
        SettingsHelper.CreateSetting(document, element, "ColorByRate", ColorByRate);
    }
}
