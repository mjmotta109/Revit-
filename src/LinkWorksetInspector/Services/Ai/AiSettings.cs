using System;
using System.IO;
using System.Text.Json;

namespace LinkWorksetInspector.Services.Ai
{
    /// <summary>
    /// Configuración del asistente IA: API key de Anthropic y modelo.
    /// La clave se toma de la variable de entorno ANTHROPIC_API_KEY si existe;
    /// si no, del archivo %AppData%\LinkWorksetInspector\ai-settings.json.
    /// </summary>
    public class AiSettings
    {
        public string ApiKey { get; set; }
        public string Model { get; set; } = "claude-opus-4-8";

        /// <summary>true si la clave vino de la variable de entorno (no se debe sobrescribir el archivo).</summary>
        public bool KeyFromEnvironment { get; private set; }

        private static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LinkWorksetInspector");
                return Path.Combine(dir, "ai-settings.json");
            }
        }

        public static AiSettings Load()
        {
            var settings = new AiSettings();

            try
            {
                if (File.Exists(SettingsPath))
                {
                    var stored = JsonSerializer.Deserialize<AiSettings>(File.ReadAllText(SettingsPath));
                    if (stored != null)
                    {
                        settings.ApiKey = stored.ApiKey;
                        if (!string.IsNullOrWhiteSpace(stored.Model)) settings.Model = stored.Model;
                    }
                }
            }
            catch { /* archivo corrupto: se ignora y se vuelve a pedir la clave */ }

            string envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                settings.ApiKey = envKey;
                settings.KeyFromEnvironment = true;
            }

            return settings;
        }

        public void Save()
        {
            if (KeyFromEnvironment) return;

            string dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
