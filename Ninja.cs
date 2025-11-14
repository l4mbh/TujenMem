using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ImGuiNET;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TujenMem;


public enum DownloadType
{
    Currency,
    Items,
    Exchange
}
public enum DownloadIntegrity
{
    Valid,
    Invalid,
    Unknown
}

public class Ninja
{
    private static Dictionary<string, List<NinjaItem>> _items = new();
    private static bool _dirty = true;
    private static Dictionary<string, float> fileProgress = new Dictionary<string, float>();
    private static Dictionary<string, DownloadIntegrity> fileIntegrity = new Dictionary<string, DownloadIntegrity>();
    private static Dictionary<string, string> fileStatus = new Dictionary<string, string>();
    private static string _currentDownloadStatus = "";
    private static bool _isDownloading = false;

    private static string DataFolder
    {
        get
        {
            if (TujenMem.Instance == null)
            {
                return Path.Combine(Directory.GetCurrentDirectory(), "NinjaData");
            }
            return Path.Combine(TujenMem.Instance.DirectoryFullName, "NinjaData");
        }
    }

    public static Dictionary<string, List<NinjaItem>> Items
    {
        get
        {
            if (_dirty)
            {
                Task.Run(Parse).Wait();
                _dirty = false;
            }
            return _items;
        }
    }

    public static void SetDirty()
    {
        _dirty = true;
    }

    private static readonly List<(string, DownloadType)> DownloadList = new List<(string, DownloadType)>
    {
        ("Currency", DownloadType.Currency),
        ("Fragment", DownloadType.Currency),
        ("Artifact", DownloadType.Exchange),
        ("Oil", DownloadType.Items),
        ("Incubator", DownloadType.Items),
        ("Map", DownloadType.Items),
        ("BlightedMap", DownloadType.Items),
        ("UniqueMap", DownloadType.Items),
        ("DeliriumOrb", DownloadType.Items),
        ("Scarab", DownloadType.Items),
        ("Fossil", DownloadType.Items),
        ("Resonator", DownloadType.Items),
        ("Essence", DownloadType.Items),
        ("SkillGem", DownloadType.Items),
        ("Tattoo", DownloadType.Items),
        ("DivinationCard", DownloadType.Items),
        ("ClusterJewel", DownloadType.Items)
    };

    // Các loại item hỗ trợ Exchange API (poe.ninja/poe1/api/economy/exchange)
    // Khi dùng nút "Download Exchange Data", các loại này sẽ được tải từ Exchange API
    // Exchange API cung cấp dữ liệu chính xác hơn cho currency và các item có thể trade trực tiếp
    private static readonly List<string> ExchangeTypes = new List<string>
    {
        "Currency",
        "Fragment",
        "Artifact",
        "Oil",
        "Scarab",
        "Fossil",
        "Essence"
    };

    public static bool IsValid
    {
        get
        {
            return fileIntegrity.Count == DownloadList.Count && fileIntegrity.Values.All(x => x == DownloadIntegrity.Valid);
        }
    }

    public static void RenderSettings()
    {
        if (ImGui.TreeNodeEx("Ninja Data"))
        {
            ValidityIndicator();

            var isDownloadDisabled = _isDownloading;
            if (isDownloadDisabled)
            {
                ImGui.BeginDisabled();
            }
            
            if (ImGui.Button("Download Data"))
            {
                _isDownloading = true;
                _currentDownloadStatus = "Đang tải dữ liệu Overview...";
                Task.Run(DownloadFilesAsync).ContinueWith((t) => { 
                    _dirty = true; 
                    CheckIntegrity(); 
                    _isDownloading = false;
                    _currentDownloadStatus = "";
                }).ContinueWith(async (t) => { await Parse(); });
            }
            ImGui.SameLine();
            if (ImGui.Button("Download Exchange Data"))
            {
                _isDownloading = true;
                _currentDownloadStatus = "Đang tải dữ liệu Exchange...";
                Task.Run(DownloadExchangeFilesAsync).ContinueWith((t) => { 
                    _dirty = true; 
                    CheckIntegrity(); 
                    _isDownloading = false;
                    _currentDownloadStatus = "";
                }).ContinueWith(async (t) => { await Parse(); });
            }
            ImGui.SameLine();
            if (ImGui.Button("Re-Check Integrity"))
            {
                CheckIntegrity();
                _dirty = true;
                Task.Run(Parse);
            }
            
            if (isDownloadDisabled)
            {
                ImGui.EndDisabled();
            }
            
            if (_isDownloading && !string.IsNullOrEmpty(_currentDownloadStatus))
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), _currentDownloadStatus);
            }
            
            if (ImGui.Button("Test URLs (Debug)"))
            {
                Task.Run(TestURLs);
            }
            
            PriceHistory.RenderButton();
            HaggleHistory.RenderButton();

            try
            {
                var itemCount = _items.Count;
                var itemText = $"{itemCount} items loaded";
                float textWidth = ImGui.CalcTextSize(itemText).X;
                float remainingWidth = ImGui.GetContentRegionAvail().X - textWidth;
                ImGui.SameLine(remainingWidth);
                ImGui.Text(itemText);
            }
            catch
            {
                var itemText = "Items loading...";
                float textWidth = ImGui.CalcTextSize(itemText).X;
                float remainingWidth = ImGui.GetContentRegionAvail().X - textWidth;
                ImGui.SameLine(remainingWidth);
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), itemText);
            }

            if (ImGui.BeginTable("File Table", 6))
            {
                ImGui.TableSetupColumn("File Name");
                ImGui.TableSetupColumn("Exists");
                ImGui.TableSetupColumn("Integrity");
                ImGui.TableSetupColumn("Age");
                ImGui.TableSetupColumn("Status");
                ImGui.TableSetupColumn("Progress");
                ImGui.TableHeadersRow();

                foreach (var file in DownloadList)
                {
                    var filePath = GetFilePathForName(file.Item1);
                    var fileName = Path.GetFileName(filePath);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(fileName);

                    ImGui.TableNextColumn();
                    var fileExists = File.Exists(filePath);
                    if (fileExists)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Yes");
                    }
                    else
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "No");
                    }


                    ImGui.TableNextColumn();
                    if (fileIntegrity.ContainsKey(filePath))
                    {
                        switch (fileIntegrity[filePath])
                        {
                            case DownloadIntegrity.Valid:
                                ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Valid");
                                break;
                            case DownloadIntegrity.Invalid:
                                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Invalid");
                                break;
                            case DownloadIntegrity.Unknown:
                                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Unknown");
                                break;
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Unknown");
                    }

                    ImGui.TableNextColumn();
                    if (fileExists)
                    {
                        DateTime lastModified = File.GetLastWriteTime(filePath);
                        TimeSpan age = DateTime.Now - lastModified;

                        string ageText;
                        if (age.TotalDays >= 1)
                        {
                            ageText = $"{(int)age.TotalDays} days, {(int)age.Hours} hours";
                        }
                        else if (age.TotalHours >= 1)
                        {
                            ageText = $"{(int)age.TotalHours} hours, {age.Minutes} minutes";
                        }
                        else if (age.TotalMinutes >= 1)
                        {
                            ageText = $"{age.Minutes} minutes";
                        }
                        else
                        {
                            ageText = $"{age.Seconds} seconds";
                        }

                        ImGui.Text(ageText);
                    }
                    else
                    {
                        ImGui.Text("-");
                    }

                    ImGui.TableNextColumn();
                    if (fileStatus.ContainsKey(filePath) && !string.IsNullOrEmpty(fileStatus[filePath]))
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 1, 1), fileStatus[filePath]);
                    }
                    else
                    {
                        ImGui.Text("-");
                    }

                    ImGui.TableNextColumn();
                    var progress = fileProgress.ContainsKey(filePath) ? fileProgress[filePath] : 0;
                    ImGui.ProgressBar(progress);
                }

                ImGui.EndTable();
            }

            ImGui.TreePop();
        }
        else
        {
            ValidityIndicator();
        }
    }

    private static void ValidityIndicator()
    {
        ImGui.SameLine();
        ImGui.Text("Validity: ");
        ImGui.SameLine();
        try
        {
            var isValid = fileIntegrity.Count == DownloadList.Count && fileIntegrity.Values.All(x => x == DownloadIntegrity.Valid);
            if (isValid)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Valid");
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Invalid");
            }
        }
        catch
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Unknown");
        }
    }

    // Thử parse file theo format Exchange API
    // Exchange API có cấu trúc: items[] ở root chứa metadata, lines[] chứa giá
    // primaryValue trong Exchange API đã là giá chaos equivalent
    private static async Task<bool> TryParseExchangeFile(string filePath, List<NinjaItem> result)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Log.Debug($"TryParseExchangeFile: File not exists: {filePath}");
                return false;
            }

            var content = await File.ReadAllTextAsync(filePath);
            Log.Debug($"TryParseExchangeFile: Content length: {content.Length} chars");
            
            var exchangeData = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONExchangeFile>(content);
            
            if (exchangeData?.Items == null || exchangeData?.Lines == null)
            {
                Log.Debug($"TryParseExchangeFile: Invalid Exchange format. Items null: {exchangeData?.Items == null}, Lines null: {exchangeData?.Lines == null}");
                return false;
            }

            // Dùng mảng Items ở root level để mapping (không phải Core.Items)
            var itemsDict = exchangeData.Items.ToDictionary(i => i.Id, i => i.Name);
            Log.Debug($"TryParseExchangeFile: Found {itemsDict.Count} items");

            int addedCount = 0;
            foreach (var line in exchangeData.Lines)
            {
                if (itemsDict.TryGetValue(line.Id, out var itemName) && line.PrimaryValue > 0)
                {
                    result.Add(new NinjaItem(itemName, line.PrimaryValue));
                    addedCount++;
                }
            }
            
            Log.Debug($"TryParseExchangeFile: Successfully parsed {addedCount} items from Exchange format");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"TryParseExchangeFile exception: {ex.Message}");
            return false;
        }
    }

    public static async Task Parse()
    {
        _items.Clear();


        _dirty = false;
        if (!IsValid)
        {
            return;
        }
        var result = new List<NinjaItem>();

        foreach (var dl in DownloadList)
        {
            var filePath = GetFilePathForName(dl.Item1);
            
            if (ExchangeTypes.Contains(dl.Item1) && await TryParseExchangeFile(filePath, result))
            {
                continue;
            }

            switch (dl.Item2)
            {
                case DownloadType.Currency:
                    var parsedCurrency = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONCurrencyLine>>(File.ReadAllText(filePath));
                    var linesCurrency = parsedCurrency.Lines.Select(l => new NinjaItem(l.CurrencyTypeName, l.ChaosEquivalent)).ToList();
                    result.AddRange(linesCurrency);
                    break;

                case DownloadType.Items:
                    var content = await File.ReadAllTextAsync(filePath);
                    if (dl.Item1 == "Map" || dl.Item1 == "UniqueMap")
                    {
                        var isUnique = dl.Item1 == "UniqueMap";
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineMap>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItemMap(l.Name, l.ChaosValue, l.MapTier, isUnique)).ToList();
                        result.AddRange(lines);
                    }
                    else if (dl.Item1 == "SkillGem")
                    {
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineGem>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItemGem(l.Name, l.ChaosValue, l.GemLevel, l.GemQuality, l.Name == "Enlighten Support" || l.Name == "Empower Support" || l.Name == "Enhance Support", l.Corrupted)).ToList();
                        result.AddRange(lines);

                    }
                    else if (dl.Item1 == "ClusterJewel")
                    {
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineClusteJewel>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItemClusterJewel(l.Name, l.ChaosValue, l.LevelRequired, int.Parse(Regex.Replace(l.Variant, "[^0-9]", "")), l.BaseType)).ToList();
                        result.AddRange(lines);
                    }
                    else
                    {
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLine>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItem(l.Name, l.ChaosValue)).ToList();
                        result.AddRange(lines);
                    }
                    break;

            }
        }

        _items = NinjaItemListToDict(result);
    }

    private static Dictionary<string, List<NinjaItem>> NinjaItemListToDict(List<NinjaItem> items)
    {
        try
        {
            var dict = items.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.ToList());
            if (TujenMem.Instance == null || TujenMem.Instance.Settings == null)
            {
                return dict;
            }
            foreach (var pr in TujenMem.Instance.Settings.CustomPrices)
            {
                var customPrice = pr.Item2 != null ? new CustomPrice(pr.Item1, pr.Item2 ?? 0f) : new CustomPrice(pr.Item1, pr.Item3);
                var item = dict.ContainsKey(customPrice.Name) ? dict[customPrice.Name].First() : new NinjaItem(customPrice.Name, 0);
                if (customPrice.Value != null)
                {
                    item.ChaosValue = (float)customPrice.Value;
                }
                else
                {
                    try
                    {
                        string replacedExpr = dict.Aggregate(customPrice.Expression, (current, pair) => current.Replace($"{{{pair.Key}}}", pair.Value.First().ChaosValue.ToString(CultureInfo.InvariantCulture)));
                        float result = Evaluate(replacedExpr);
                        item.ChaosValue = result;
                    }
                    catch (Exception e)
                    {
                        Log.Error(customPrice.Name + ":" + customPrice.Expression + " - " + e.Message);
                    }
                }
                dict[customPrice.Name] = new List<NinjaItem> { item };
            }
            return dict;
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            return new();
        }
    }
    private static float Evaluate(string expression)
    {
        DataTable table = new();
        table.Columns.Add("expression", typeof(string), expression);
        DataRow row = table.NewRow();
        table.Rows.Add(row);
        return float.Parse((string)row["expression"]);
    }

    public static void CheckIntegrity()
    {
        foreach (var dl in DownloadList)
        {
            var filePath = GetFilePathForName(dl.Item1);
            if (!File.Exists(filePath))
            {
                fileIntegrity[filePath] = DownloadIntegrity.Invalid;
                continue;
            }
            try
            {
                string text = File.ReadAllText(filePath);
                JToken.Parse(text);
                fileIntegrity[filePath] = DownloadIntegrity.Valid;
            }
            catch
            {
                fileIntegrity[filePath] = DownloadIntegrity.Invalid;
            }
        }
    }

    private static async Task DownloadFileAsync(string url, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
        
        using (HttpClient client = new HttpClient(handler))
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Referer", "https://poe.ninja/");
            
            try
            {
                Log.Debug($"[{fileName}] Bắt đầu tải từ: {url}");
                fileStatus[filePath] = "Connecting...";
                fileProgress[filePath] = 0.1f;
                
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                Log.Debug($"[{fileName}] Nhận được response, status: {response.StatusCode}");
                fileStatus[filePath] = "Downloading...";
                fileProgress[filePath] = 0.3f;
                
                var content = await response.Content.ReadAsStringAsync();
                Log.Debug($"[{fileName}] Tải xong {content.Length} ký tự");
                fileStatus[filePath] = "Saving...";
                fileProgress[filePath] = 0.6f;
                
                if (content.Length == 0)
                {
                    throw new Exception($"Response rỗng từ {url}");
                }
                
                if (content.Length < 500)
                {
                    Log.Debug($"[{fileName}] Nội dung: {content}");
                }
                else
                {
                    Log.Debug($"[{fileName}] Preview (500 ký tự đầu): {content.Substring(0, 500)}...");
                }
                
                await File.WriteAllTextAsync(filePath, content);
                fileStatus[filePath] = "Validating...";
                fileProgress[filePath] = 0.9f;
                
                try
                {
                    JToken.Parse(content);
                    Log.Debug($"[{fileName}] ✓ JSON hợp lệ, đã lưu vào: {filePath}");
                    fileStatus[filePath] = "✓ Done";
                    fileProgress[filePath] = 1.0f;
                    fileIntegrity[filePath] = DownloadIntegrity.Valid;
                }
                catch (Exception jsonEx)
                {
                    Log.Error($"[{fileName}] ✗ JSON không hợp lệ: {jsonEx.Message}");
                    fileStatus[filePath] = "✗ Invalid JSON";
                    fileProgress[filePath] = 0.9f;
                    fileIntegrity[filePath] = DownloadIntegrity.Invalid;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Log.Error($"[{fileName}] ✗ Lỗi HTTP: {httpEx.Message}");
                Log.Error($"[{fileName}] URL: {url}");
                fileStatus[filePath] = $"✗ HTTP Error";
                fileProgress[filePath] = 0f;
                fileIntegrity[filePath] = DownloadIntegrity.Invalid;
                throw;
            }
            catch (TaskCanceledException)
            {
                Log.Error($"[{fileName}] ✗ Timeout khi tải từ: {url}");
                fileStatus[filePath] = "✗ Timeout";
                fileProgress[filePath] = 0f;
                fileIntegrity[filePath] = DownloadIntegrity.Invalid;
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[{fileName}] ✗ Lỗi: {ex.Message}");
                Log.Error($"[{fileName}] Stack trace: {ex.StackTrace}");
                fileStatus[filePath] = "✗ Error";
                fileProgress[filePath] = 0f;
                fileIntegrity[filePath] = DownloadIntegrity.Invalid;
                throw;
            }
        }
    }

    private static async Task DownloadFilesAsync()
    {
        if (!Directory.Exists(DataFolder))
        {
            Directory.CreateDirectory(DataFolder);
        }
        
        Log.Debug($"========================================");
        Log.Debug($"Bắt đầu tải {DownloadList.Count} files từ poe.ninja");
        Log.Debug($"League: {TujenMem.Instance?.Settings?.League?.Value ?? "Unknown"}");
        Log.Debug($"========================================");
        
        var tasks = new List<Task>();
        var failedDownloads = new List<string>();
        
        foreach (var dl in DownloadList)
        {
            var filePath = GetFilePathForName(dl.Item1);
            var url = GetUrlForDownloadFile(dl);
            fileProgress[filePath] = 0;
            
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await DownloadFileAsync(url, filePath);
                }
                catch (Exception ex)
                {
                    lock (failedDownloads)
                    {
                        failedDownloads.Add($"{dl.Item1}: {ex.Message}");
                    }
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        Log.Debug($"========================================");
        Log.Debug($"Hoàn tất tải dữ liệu");
        
        var successCount = DownloadList.Count - failedDownloads.Count;
        Log.Debug($"Thành công: {successCount}/{DownloadList.Count}");
        
        if (failedDownloads.Count > 0)
        {
            Log.Error($"Thất bại: {failedDownloads.Count} files:");
            foreach (var failed in failedDownloads)
            {
                Log.Error($"  - {failed}");
            }
        }
        else
        {
            Log.Debug($"✓ Tất cả files đã tải thành công!");
        }
        Log.Debug($"========================================");
    }

    private static async Task DownloadExchangeFilesAsync()
    {
        if (!Directory.Exists(DataFolder))
        {
            Directory.CreateDirectory(DataFolder);
        }
        
        var league = TujenMem.Instance?.Settings?.League?.Value ?? "Ancestor";
        
        Log.Debug($"========================================");
        Log.Debug($"Bắt đầu tải {ExchangeTypes.Count} Exchange files từ poe.ninja");
        Log.Debug($"League: {league}");
        Log.Debug($"========================================");
        
        var tasks = new List<Task>();
        var failedDownloads = new List<string>();
        
        foreach (var type in ExchangeTypes)
        {
            var filePath = GetFilePathForName(type);
            var url = GetExchangeUrl(type);
            fileProgress[filePath] = 0;
            
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await DownloadFileAsync(url, filePath);
                }
                catch (Exception ex)
                {
                    lock (failedDownloads)
                    {
                        failedDownloads.Add($"{type}: {ex.Message}");
                    }
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        Log.Debug($"========================================");
        Log.Debug($"Hoàn tất tải Exchange data");
        
        var successCount = ExchangeTypes.Count - failedDownloads.Count;
        Log.Debug($"Thành công: {successCount}/{ExchangeTypes.Count}");
        
        if (failedDownloads.Count > 0)
        {
            Log.Error($"Thất bại: {failedDownloads.Count} files:");
            foreach (var failed in failedDownloads)
            {
                Log.Error($"  - {failed}");
            }
        }
        else
        {
            Log.Debug($"✓ Tất cả Exchange files đã tải thành công!");
        }
        Log.Debug($"========================================");
        
        await ValidateExchangeData();
    }
    
    private static async Task ValidateExchangeData()
    {
        try
        {
            var testFile = GetFilePathForName("Currency");
            if (File.Exists(testFile))
            {
                var content = await File.ReadAllTextAsync(testFile);
                var testData = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONExchangeFile>(content);
                
                if (testData?.Items == null || testData.Items.Count == 0 || testData?.Lines == null || testData.Lines.Count == 0)
                {
                    var league = TujenMem.Instance?.Settings?.League?.Value ?? "Unknown";
                    Log.Error($"========================================");
                    Log.Error($"Exchange API returned EMPTY data for league '{league}'!");
                    Log.Error($"Response structure exists but arrays are empty:");
                    Log.Error($"  - core.items: {testData?.Core?.Items?.Count ?? 0}");
                    Log.Error($"  - items: {testData?.Items?.Count ?? 0}");
                    Log.Error($"  - lines: {testData?.Lines?.Count ?? 0}");
                    Log.Error($"");
                    Log.Error($"This means league name '{league}' is INCORRECT or has no data.");
                    Log.Error($"");
                    Log.Error($"Common league names to try:");
                    Log.Error($"  - Standard (permanent softcore)");
                    Log.Error($"  - Hardcore (permanent hardcore)");
                    Log.Error($"  - SSF Standard");
                    Log.Error($"  - Check current challenge league at: https://poe.ninja");
                    Log.Error($"");
                    Log.Error($"Test URL in browser to verify:");
                    Log.Error($"  https://poe.ninja/poe1/api/economy/exchange/current/overview?league={league}&type=Currency");
                    Log.Error($"========================================");
                }
                else
                {
                    Log.Debug($"✓ Exchange API validation SUCCESS!");
                    Log.Debug($"  - Items metadata: {testData.Items.Count}");
                    Log.Debug($"  - Price entries: {testData.Lines.Count}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error validating Exchange data: {ex.Message}");
        }
    }

    private static string GetFilePathForName(string name)
    {
        return Path.Join(DataFolder, name + ".json");
    }

    private static string GetUrlForDownloadFile((string, DownloadType) dl)
    {
        var league = TujenMem.Instance?.Settings?.League?.Value ?? "Ancestor";
        
        switch (dl.Item2)
        {
            case DownloadType.Currency:
                return $"https://poe.ninja/api/data/CurrencyOverview?league={league}&type={dl.Item1}&language=en";
            case DownloadType.Items:
                return $"https://poe.ninja/api/data/ItemOverview?league={league}&type={dl.Item1}&language=en";
            case DownloadType.Exchange:
                return GetExchangeUrl(dl.Item1);
            default:
                throw new Exception("Unknown DownloadType");
        }
    }

    private static string GetExchangeUrl(string type)
    {
        var league = TujenMem.Instance?.Settings?.League?.Value ?? "Ancestor";
        return $"https://poe.ninja/poe1/api/economy/exchange/current/overview?league={league}&type={type}";
    }

    private static async Task TestURLs()
    {
        var league = TujenMem.Instance?.Settings?.League?.Value ?? "Unknown";
        
        Log.Debug($"========================================");
        Log.Debug($"BẮT ĐẦU TEST URLs");
        Log.Debug($"League: {league}");
        Log.Debug($"========================================");
        
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
        
        using (var client = new HttpClient(handler))
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            
            foreach (var dl in DownloadList.Take(5))
            {
                var url = GetUrlForDownloadFile(dl);
                
                try
                {
                    Log.Debug($"");
                    Log.Debug($"Testing: {dl.Item1} ({dl.Item2})");
                    Log.Debug($"URL: {url}");
                    
                    var response = await client.GetAsync(url);
                    var statusCode = (int)response.StatusCode;
                    
                    Log.Debug($"  Status: {statusCode} {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        Log.Debug($"  Content Length: {content.Length} chars");
                        
                        if (content.Length > 0)
                        {
                            if (content.StartsWith("{") || content.StartsWith("["))
                            {
                                Log.Debug($"  ✓ Có vẻ là JSON");
                                
                                try
                                {
                                    JToken.Parse(content);
                                    Log.Debug($"  ✓ JSON hợp lệ");
                                    
                                    if (content.Length < 300)
                                    {
                                        Log.Debug($"  Preview: {content}");
                                    }
                                    else
                                    {
                                        Log.Debug($"  Preview (300 chars): {content.Substring(0, 300)}...");
                                    }
                                }
                                catch (Exception jsonEx)
                                {
                                    Log.Error($"  ✗ JSON không hợp lệ: {jsonEx.Message}");
                                }
                            }
                            else if (content.Contains("<html") || content.Contains("<!DOCTYPE"))
                            {
                                Log.Error($"  ✗ Response là HTML, không phải JSON!");
                                Log.Error($"  → League name có thể SAI hoặc API đã thay đổi");
                                Log.Error($"  Preview: {content.Substring(0, Math.Min(200, content.Length))}...");
                            }
                            else
                            {
                                Log.Error($"  ⚠ Response không phải JSON hay HTML");
                                Log.Debug($"  Preview: {content.Substring(0, Math.Min(200, content.Length))}...");
                            }
                        }
                        else
                        {
                            Log.Error($"  ✗ Response rỗng!");
                        }
                    }
                    else
                    {
                        Log.Error($"  ✗ HTTP Error: {statusCode} {response.StatusCode}");
                        var errorContent = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(errorContent))
                        {
                            Log.Error($"  Error content: {errorContent.Substring(0, Math.Min(200, errorContent.Length))}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"  ✗ Exception: {ex.Message}");
                }
            }
            
            Log.Debug($"");
            Log.Debug($"========================================");
            Log.Debug($"KẾT THÚC TEST URLs");
            Log.Debug($"Chỉ test 5 URLs đầu tiên. Nếu có lỗi, kiểm tra League name!");
            Log.Debug($"========================================");
        }
    }

}