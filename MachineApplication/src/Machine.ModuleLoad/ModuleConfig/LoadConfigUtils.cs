using System.IO;
using System.Xml.Serialization;
using Machine.ModuleLoad.Utils;
using RustSharp;

namespace Machine.ModuleLoad.ModuleConfig;

internal static class LoadConfigUtils {
    internal static Result<ModuleLoadSources.Models.ModuleConfig, string> LoadFromFile(string configPath,
        string configName = "modules.xml") {
        try {
            ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(configName);
            var filePath = Path.Combine(configPath, configName);
            if (!File.Exists(filePath))
                return Result<ModuleLoadSources.Models.ModuleConfig, string>.Err(
                    $"Module configuration file was not found: {filePath}");
            return Deserialize(File.ReadAllText(filePath));
        }
        catch (Exception ex) {
            return Result<ModuleLoadSources.Models.ModuleConfig, string>.Err(ex.Message);
        }
    }

    internal static Result<ModuleLoadSources.Models.ModuleConfig, string> Deserialize(string moduleConfigStr) {
        return moduleConfigStr.XmlToModel<ModuleLoadSources.Models.ModuleConfig>().Match(
            Result<ModuleLoadSources.Models.ModuleConfig, string>.Ok,
            err => Result<ModuleLoadSources.Models.ModuleConfig, string>.Err(err.Message));
        try {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleConfigStr);
            var serializer = new XmlSerializer(typeof(ModuleLoadSources.Models.ModuleConfig));
            using var reader = new StringReader(moduleConfigStr);
            var config = serializer.Deserialize(reader) as ModuleLoadSources.Models.ModuleConfig;
            return config is null
                ? Result<ModuleLoadSources.Models.ModuleConfig, string>.Err(
                    "The module configuration did not contain a valid wpfModules root element.")
                : Result<ModuleLoadSources.Models.ModuleConfig, string>.Ok(config);
        }
        catch (Exception ex) {
            return Result<ModuleLoadSources.Models.ModuleConfig, string>.Err(ex.Message);
        }
    }
}