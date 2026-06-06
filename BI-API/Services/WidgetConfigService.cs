using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bimasakti.BiService.Api.Models;

namespace Bimasakti.BiService.Api.Services
{
    public interface IWidgetConfigService
    {
        List<DsbiWidgetConfig> GetAvailableWidgets();
        DsbiWidgetConfig? GetWidgetConfig(string category, string id);
        bool SaveWidgetConfig(DsbiWidgetConfig config);
        bool DeleteWidgetConfig(string category, string id);
    }

    public class WidgetConfigService : IWidgetConfigService
    {
        private readonly string _widgetsDirectory;

        public WidgetConfigService()
        {
            _widgetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Widgets");
            if (!Directory.Exists(_widgetsDirectory))
            {
                Directory.CreateDirectory(_widgetsDirectory);
            }
        }

        public List<DsbiWidgetConfig> GetAvailableWidgets()
        {
            var widgets = new List<DsbiWidgetConfig>();

            if (!Directory.Exists(_widgetsDirectory))
                return widgets;

            var files = Directory.GetFiles(_widgetsDirectory, "*.dsbi", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var config = JsonSerializer.Deserialize<DsbiWidgetConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (config != null)
                    {
                        widgets.Add(config);
                    }
                }
                catch
                {
                    // Ignore malformed files
                }
            }

            return widgets;
        }

        public DsbiWidgetConfig? GetWidgetConfig(string category, string id)
        {
            var catDir = Path.Combine(_widgetsDirectory, category);
            var filePath = Path.Combine(catDir, $"{id}.dsbi");

            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<DsbiWidgetConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { }
            }
            return null;
        }

        public bool SaveWidgetConfig(DsbiWidgetConfig config)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(config.Category)) config.Category = "Uncategorized";
                var catDir = Path.Combine(_widgetsDirectory, config.Category);
                if (!Directory.Exists(catDir))
                {
                    Directory.CreateDirectory(catDir);
                }

                var filePath = Path.Combine(catDir, $"{config.Id}.dsbi");
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteWidgetConfig(string category, string id)
        {
            try
            {
                var filePath = Path.Combine(_widgetsDirectory, category ?? "Uncategorized", $"{id}.dsbi");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
