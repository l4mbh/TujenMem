using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExileCore.PoEMemory.Components;

namespace TujenMem;

public class NinjaItem
{
  public string Name { get; set; }
  public float ChaosValue { get; set; }

  public NinjaItem(string name, float chaosValue)
  {
    Name = name;
    ChaosValue = chaosValue;
  }
}

public class NinjaItemMap : NinjaItem
{
  public int Tier { get; set; }
  public bool Influenced { get; set; }

  public bool Unique { get; set; }

  public NinjaItemMap(string name, float chaosValue, int tier, bool unique) : base(name, chaosValue)
  {
    Tier = tier;
    Unique = unique;
  }
}

public class NinjaItemGem : NinjaItem
{
  public int Level { get; set; }
  public int Quality { get; set; }

  public bool SpecialSupport { get; set; }
  public bool Corrupted { get; set; }

  public NinjaItemGem(string name, float chaosValue, int level, int quality, bool specialSupport, bool corrupted) : base(name, chaosValue)
  {
    Level = level;
    Quality = quality;
    SpecialSupport = specialSupport;
    Corrupted = corrupted;
  }
}

public class NinjaItemClusterJewel : NinjaItem
{
  public int ItemLevel { get; set; }
  public int PassiveSkills { get; set; }
  public string BaseType { get; set; }

  public NinjaItemClusterJewel(string name, float chaosValue, int itemLevel, int passiveSkills, string baseType) : base(name, chaosValue)
  {
    ItemLevel = itemLevel;
    PassiveSkills = passiveSkills;
    BaseType = baseType;
  }
}

public class JSONCurrencyLine
{
  public string CurrencyTypeName { get; set; }
  public float ChaosEquivalent { get; set; }
}

public class JSONItemLine
{
  public string Name { get; set; }
  public float ChaosValue { get; set; }
}

public class JSONItemLineMap : JSONItemLine
{
  public int MapTier { get; set; }
  public int ItemClass { get; set; }
}

public class JSONItemLineGem : JSONItemLine
{
  public int GemLevel { get; set; }
  public int GemQuality { get; set; }
  public bool Corrupted { get; set; }
}

public class JSONItemLineClusteJewel : JSONItemLine
{
  public int LevelRequired { get; set; }
  public string Variant { get; set; }
  public string BaseType { get; set; }
}

public class JSONFile<T>
{
  public List<T> Lines { get; set; }
}

public class JSONExchangeCoreItem
{
  public string Id { get; set; }
  public string Name { get; set; }
  public string Image { get; set; }
  public string Category { get; set; }
  public string DetailsId { get; set; }
}

public class JSONExchangeCoreRates
{
  public float Divine { get; set; }
}

public class JSONExchangeCore
{
  public List<JSONExchangeCoreItem> Items { get; set; }
  public JSONExchangeCoreRates Rates { get; set; }
  public string Primary { get; set; }
  public string Secondary { get; set; }
}

public class JSONExchangeLine
{
  public string Id { get; set; }
  public float PrimaryValue { get; set; }
}

public class JSONExchangeFile
{
  public JSONExchangeCore Core { get; set; }
  public List<JSONExchangeLine> Lines { get; set; }
  public List<JSONExchangeCoreItem> Items { get; set; }
}


public class FetchNinja
{
  public FetchNinja(string league, string cachePath)
  {
    _league = league;
    _cachePath = cachePath;
    CurrencyOverview.Add("Currency");
    CurrencyOverview.Add("Fragment");

    ItemOverview.Add("Oil");
    ItemOverview.Add("Incubator");
    ItemOverview.Add("Map");
    ItemOverview.Add("BlightedMap");
    ItemOverview.Add("UniqueMap");
    ItemOverview.Add("DeliriumOrb");
    ItemOverview.Add("Scarab");
    ItemOverview.Add("Fossil");
    ItemOverview.Add("Resonator");
    ItemOverview.Add("Essence");
    ItemOverview.Add("SkillGem");
    ItemOverview.Add("Tattoo");
    ItemOverview.Add("DivinationCard");
    ItemOverview.Add("ClusterJewel");
  }

  private readonly string _league = null;
  private readonly string _cachePath = null;

  private readonly List<string> CurrencyOverview = new();
  private readonly List<string> ItemOverview = new();

  public async Task<string> Fetch()
  {
    foreach (var currencyType in CurrencyOverview)
    {
      var url = GetCurrencyOverviewURL(currencyType);
      var path = Path.Join(_cachePath, "Data", currencyType + ".json");
      var result = await DownloadToFile(url, path);
      if (result != null)
      {
        return result;
      }
    }

    foreach (var itemType in ItemOverview)
    {
      var url = GetItemOverviewURL(itemType);
      var path = Path.Join(_cachePath, "Data", itemType + ".json");
      var result = await DownloadToFile(url, path);
      if (result != null)
      {
        return result;
      }
    }
    return null;
  }

  public bool CheckIfShouldFetch()
  {
    var dataPath = _cachePath + "\\Data";
    if (!Directory.Exists(dataPath))
    {
      return true;
    }

    if (!CheckIfCanParse)
    {
      return true;
    }

    var files = Directory.GetFiles(dataPath);
    if (files.Length == 0)
    {
      return true;
    }



    var now = DateTime.Now;
    var oldestFile = files.Select(f => new FileInfo(f)).OrderBy(f => f.LastWriteTime).First();
    var timeSinceLastWrite = now - oldestFile.LastWriteTime;
    if (timeSinceLastWrite.TotalMinutes > 60)
    {
      return true;
    }

    return false;
  }

  public bool CheckIfCanParse => CurrencyOverview.All(c => File.Exists(Path.Join(_cachePath, "Data", c + ".json"))) && ItemOverview.All(c => File.Exists(Path.Join(_cachePath, "Data", c + ".json")));

  public async Task<List<NinjaItem>> ParseNinjaItems()
  {
    var result = new List<NinjaItem>();
    foreach (var currencyType in CurrencyOverview)
    {
      var path = Path.Join(_cachePath, "Data", currencyType + ".json");
      var content = await File.ReadAllTextAsync(path);
      var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONCurrencyLine>>(content);
      var lines = parsed.Lines.Select(l => new NinjaItem(l.CurrencyTypeName, l.ChaosEquivalent)).ToList();
      result.AddRange(lines);
    }
    foreach (var itemType in ItemOverview)
    {
      var path = Path.Join(_cachePath, "Data", itemType + ".json");
      var content = await File.ReadAllTextAsync(path);
      if (itemType == "Map" || itemType == "UniqueMap")
      {
        var isUnique = itemType == "UniqueMap";
        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineMap>>(content);
        var lines = parsed.Lines.Select(l => new NinjaItemMap(l.Name, l.ChaosValue, l.MapTier, isUnique)).ToList();
        result.AddRange(lines);
      }
      else if (itemType == "SkillGem")
      {
        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineGem>>(content);
        var lines = parsed.Lines.Select(l => new NinjaItemGem(l.Name, l.ChaosValue, l.GemLevel, l.GemQuality, l.Name == "Enlighten Support" || l.Name == "Empower Support" || l.Name == "Enhance Support", l.Corrupted)).ToList();
        result.AddRange(lines);

      }
      else if (itemType == "ClusterJewel")
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
    }
    return result;
  }

  private async Task<string> DownloadToFile(string Url, string Path)
  {
    try
    {
      var handler = new HttpClientHandler
      {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10
      };
      
      using var client = new HttpClient(handler);
      client.Timeout = TimeSpan.FromMinutes(5);
      
      // Thêm headers để giả lập browser request
      client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
      client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
      client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
      client.DefaultRequestHeaders.Add("Referer", "https://poe.ninja/");
      
      var response = await client.GetAsync(Url);
      response.EnsureSuccessStatusCode();
      var content = await response.Content.ReadAsStringAsync();

      var dataPath = _cachePath + "\\Data";
      if (!Directory.Exists(dataPath))
      {
        Directory.CreateDirectory(dataPath);
      }
      await File.WriteAllTextAsync(Path, content);
      return null;
    }
    catch (HttpRequestException e)
    {
      return e.Message;
    }
  }


  private string GetCurrencyOverviewURL(string CurrencyType)
  {
    return "https://poe.ninja/api/data/CurrencyOverview?league=" + _league + "&type=" + CurrencyType + "&language=en";
  }

  private string GetItemOverviewURL(string ItemType)
  {
    return "https://poe.ninja/api/data/ItemOverview?league=" + _league + "&type=" + ItemType + "&language=en";
  }
}