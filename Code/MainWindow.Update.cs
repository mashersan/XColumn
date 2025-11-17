using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using XColumn.Models;

namespace XColumn
{
    public partial class MainWindow
    {
        private async Task CheckForUpdatesAsync(string skippedVersion)
        {
            await Task.Delay(3000);
            try
            {
                string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
                Version current = new Version(ver);

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XColumn", ver));
                    var res = await client.GetAsync("https://api.github.com/repos/mashersan/XColumn/releases/latest");
                    if (!res.IsSuccessStatusCode) return;

                    var json = JsonNode.Parse(await res.Content.ReadAsStringAsync());
                    if (json == null) return;

                    string tag = json["tag_name"]?.GetValue<string>() ?? "v0.0.0";
                    string url = json["html_url"]?.GetValue<string>() ?? "";
                    string body = json["body"]?.GetValue<string>() ?? "";
                    string remoteVerStr = tag.TrimStart('v');
                    Version remote = new Version(remoteVerStr);

                    if (remote > current && remoteVerStr != skippedVersion)
                    {
                        if (System.Windows.MessageBox.Show($"新バージョン {tag} があります。\n\n{body}\n\n更新ページを開きますか？",
                            "更新通知", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        }
                        else
                        {
                            AppSettings s = ReadSettingsFromFile(_activeProfileName);
                            s.SkippedVersion = remoteVerStr;
                            SaveSettings(_activeProfileName);
                        }
                    }
                }
            }
            catch { }
        }
    }
}