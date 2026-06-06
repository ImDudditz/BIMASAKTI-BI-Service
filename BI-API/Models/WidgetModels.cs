using System.Collections.Generic;

namespace Bimasakti.BiService.Api.Models
{
    public class DsbiWidgetConfig
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "echarts_bar";
        public DsbiQueryConfig Query { get; set; } = new();
        public Dictionary<string, object> Layout { get; set; } = new();
        public Dictionary<string, object> Formatting { get; set; } = new();
    }

    public class DsbiQueryConfig
    {
        public string Table { get; set; } = "";
        public List<string> Dimensions { get; set; } = new();
        public List<DsbiMeasure> Measures { get; set; } = new();
        public List<DsbiFilter> Filters { get; set; } = new();
    }

    public class DsbiMeasure
    {
        public string Field { get; set; } = "";
        public string Agg { get; set; } = "Sum"; // Sum, Count, Avg, Min, Max
    }

    public class DsbiFilter
    {
        public string Field { get; set; } = "";
        public string Operator { get; set; } = "=";
        public string Value { get; set; } = "";
    }
}
