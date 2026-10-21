using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AutomationTools.Models.Persistence;

namespace AutomationTools.Services
{
    public class PersistenceService
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutomationTools");
        private static readonly string SettingsFile = Path.Combine(AppDataFolder, "Workspaces.json");

        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public async Task SaveAsync(AppStateDto appState)
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                string json = JsonSerializer.Serialize(appState, _options);
                string tempFile = SettingsFile + ".tmp";
                await File.WriteAllTextAsync(tempFile, json);
                File.Move(tempFile, SettingsFile, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public async Task<AppStateDto?> LoadAsync()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(SettingsFile);
                return JsonSerializer.Deserialize<AppStateDto>(json, _options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                return null;
            }
        }
    }
}
