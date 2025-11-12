using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ImGuiNET;

namespace TujenMem;

public class HaggleSessionItem
{
  public long ItemAddress { get; set; }
  public string ItemName { get; set; }
  public string ItemType { get; set; }
  public float ChaosValue { get; set; }
  public float ArtifactCost { get; set; }
  public string ArtifactType { get; set; }
  public int ArtifactAmount { get; set; }
  public int Amount { get; set; }
  public HaggleItemState State { get; set; }
  public string Details { get; set; }
  public string RejectReason { get; set; }
}

public class HaggleSession
{
  public DateTime StartTime { get; set; }
  public DateTime EndTime { get; set; }
  public int TotalRolls { get; set; }
  public int CoinsSpent { get; set; }
  public float CoinageValueAtTime { get; set; }
  public int LesserUsed { get; set; }
  public int GreaterUsed { get; set; }
  public int GrandUsed { get; set; }
  public int ExceptionalUsed { get; set; }
  public List<HaggleSessionItem> Items { get; set; } = new();
  
  public float TotalChaosValue => Items.Where(x => x.State == HaggleItemState.Bought).Sum(x => x.ChaosValue * x.Amount);
  public float TotalCostInChaos => Items.Where(x => x.State == HaggleItemState.Bought).Sum(x => x.ArtifactCost);
  public float RerollCostInChaos => TotalRolls * CoinageValueAtTime;
  public float TotalCostWithRerolls => TotalCostInChaos + RerollCostInChaos;
  public int ItemsBought => Items.Count(x => x.State == HaggleItemState.Bought);
  public int ItemsRejected => Items.Count(x => x.State == HaggleItemState.Rejected);
  public int ItemsTooExpensive => Items.Count(x => x.State == HaggleItemState.TooExpensive);
  public float ProfitInChaos => TotalChaosValue - TotalCostWithRerolls;
  public float ProfitPercent => TotalCostWithRerolls > 0 ? (ProfitInChaos / TotalCostWithRerolls) * 100 : 0;
}

public static class HaggleHistory
{
  private static string DataFolder => Path.Combine(TujenMem.Instance.DirectoryFullName, "HaggleHistory");
  private static string DataFileName => Path.Combine(DataFolder, "Sessions.csv");
  private static string ItemsFileName => Path.Combine(DataFolder, "Items.csv");
  
  private static HaggleSession _currentSession = null;
  private static List<HaggleSession> _sessions = new();
  private static bool _sessionsLoaded = false;
  
  public static void StartSession()
  {
    _currentSession = new HaggleSession
    {
      StartTime = DateTime.Now,
      CoinsSpent = 0,
      CoinageValueAtTime = GetExoticCoinagePrice()
    };
    
    if (HaggleStock.Coins > 0)
    {
      _currentSession.CoinsSpent = HaggleStock.Coins;
    }
  }
  
  private static float GetExoticCoinagePrice()
  {
    try
    {
      var possibleNames = new[]
      {
        "Exotic Coinage",
        "Exotic Coin",
        "Exotic",
        "Coinage"
      };
      
      foreach (var name in possibleNames)
      {
        if (Ninja.Items.ContainsKey(name))
        {
          var coinage = Ninja.Items[name].FirstOrDefault();
          if (coinage != null && coinage.ChaosValue > 0)
          {
            Log.Debug($"Found Exotic Coinage price: {coinage.ChaosValue}c (name: '{name}')");
            return coinage.ChaosValue;
          }
        }
      }
      
      var matchingItems = Ninja.Items.Keys.Where(k => 
        k.Contains("Exotic", StringComparison.OrdinalIgnoreCase) || 
        k.Contains("Coin", StringComparison.OrdinalIgnoreCase)
      ).ToList();
      
      if (matchingItems.Any())
      {
        Log.Debug($"Found {matchingItems.Count} items matching 'Exotic' or 'Coin': {string.Join(", ", matchingItems)}");
        var firstMatch = Ninja.Items[matchingItems[0]].FirstOrDefault();
        if (firstMatch != null && firstMatch.ChaosValue > 0)
        {
          Log.Debug($"Using first match: '{matchingItems[0]}' = {firstMatch.ChaosValue}c");
          return firstMatch.ChaosValue;
        }
      }
      
      Log.Error("Exotic Coinage not found in Ninja.Items! Using fallback price 0.5c. You can set custom price for 'Exotic Coinage' in Custom Prices settings.");
    }
    catch (Exception ex)
    {
      Log.Error($"Error getting Exotic Coinage price: {ex.Message}");
    }
    
    return 0.5f;
  }
  
  public static void RecordRoll()
  {
    if (_currentSession != null)
    {
      _currentSession.TotalRolls++;
    }
  }
  
  public static void RecordArtifactUsage(int lesser, int greater, int grand, int exceptional)
  {
    if (_currentSession != null)
    {
      _currentSession.LesserUsed += lesser;
      _currentSession.GreaterUsed += greater;
      _currentSession.GrandUsed += grand;
      _currentSession.ExceptionalUsed += exceptional;
    }
  }
  
  public static void RecordItem(HaggleItem item, string rejectReason = "")
  {
    if (_currentSession == null)
      return;
    
    var existingItem = _currentSession.Items.FirstOrDefault(x => x.ItemAddress == item.Address);
    
    if (existingItem != null)
    {
      if (existingItem.State == HaggleItemState.Rejected)
      {
        return;
      }
      
      existingItem.State = item.State;
      existingItem.ChaosValue = item.Amount > 0 ? item.Value / item.Amount : item.Value;
      existingItem.ArtifactCost = item.Price?.TotalValue() ?? 0;
      existingItem.ArtifactType = item.Price?.Name ?? "";
      existingItem.ArtifactAmount = item.Price?.Value ?? 0;
      
      if (!string.IsNullOrEmpty(rejectReason))
      {
        existingItem.RejectReason = rejectReason;
      }
    }
    else
    {
      var sessionItem = new HaggleSessionItem
      {
        ItemAddress = item.Address,
        ItemName = item.Name,
        ItemType = item.Type,
        ChaosValue = item.Amount > 0 ? item.Value / item.Amount : item.Value,
        ArtifactCost = item.Price?.TotalValue() ?? 0,
        ArtifactType = item.Price?.Name ?? "",
        ArtifactAmount = item.Price?.Value ?? 0,
        Amount = item.Amount,
        State = item.State,
        Details = GetItemDetails(item),
        RejectReason = rejectReason
      };
      
      _currentSession.Items.Add(sessionItem);
    }
  }
  
  public static void EndSession()
  {
    if (_currentSession == null)
      return;
    
    _currentSession.EndTime = DateTime.Now;
    SaveSession(_currentSession);
    _sessions.Insert(0, _currentSession);
    _currentSession = null;
  }
  
  private static void SaveSession(HaggleSession session)
  {
    try
    {
      if (!Directory.Exists(DataFolder))
      {
        Directory.CreateDirectory(DataFolder);
      }
      
      var sessionId = session.StartTime.ToString("yyyyMMdd_HHmmss");
      
      if (!File.Exists(DataFileName))
      {
        using (var sw = new StreamWriter(DataFileName, false))
        {
          sw.WriteLine("SessionId;StartTime;EndTime;TotalRolls;CoinsSpent;CoinageValue;RerollCost;LesserUsed;GreaterUsed;GrandUsed;ExceptionalUsed;ItemsBought;ItemsRejected;ItemsTooExpensive;TotalChaosValue;TotalCostInChaos;TotalCostWithRerolls;ProfitInChaos;ProfitPercent");
        }
      }
      
      using (var sw = new StreamWriter(DataFileName, true))
      {
        sw.WriteLine($"{sessionId};{session.StartTime:yyyy-MM-dd HH:mm:ss};{session.EndTime:yyyy-MM-dd HH:mm:ss};{session.TotalRolls};{session.CoinsSpent};{session.CoinageValueAtTime.ToString(CultureInfo.InvariantCulture)};{session.RerollCostInChaos.ToString(CultureInfo.InvariantCulture)};{session.LesserUsed};{session.GreaterUsed};{session.GrandUsed};{session.ExceptionalUsed};{session.ItemsBought};{session.ItemsRejected};{session.ItemsTooExpensive};{session.TotalChaosValue.ToString(CultureInfo.InvariantCulture)};{session.TotalCostInChaos.ToString(CultureInfo.InvariantCulture)};{session.TotalCostWithRerolls.ToString(CultureInfo.InvariantCulture)};{session.ProfitInChaos.ToString(CultureInfo.InvariantCulture)};{session.ProfitPercent.ToString(CultureInfo.InvariantCulture)}");
      }
      
      if (!File.Exists(ItemsFileName))
      {
        using (var sw = new StreamWriter(ItemsFileName, false))
        {
          sw.WriteLine("SessionId;ItemName;ItemType;Details;ChaosValue;ArtifactCost;ArtifactType;ArtifactAmount;Amount;State;RejectReason");
        }
      }
      
      using (var sw = new StreamWriter(ItemsFileName, true))
      {
        foreach (var item in session.Items)
        {
          sw.WriteLine($"{sessionId};{item.ItemName};{item.ItemType};{item.Details};{item.ChaosValue.ToString(CultureInfo.InvariantCulture)};{item.ArtifactCost.ToString(CultureInfo.InvariantCulture)};{item.ArtifactType};{item.ArtifactAmount};{item.Amount};{item.State};{item.RejectReason}");
        }
      }
    }
    catch (Exception ex)
    {
      Log.Error($"Error saving haggle session: {ex.Message}");
    }
  }
  
  private static void LoadSessions()
  {
    if (_sessionsLoaded)
      return;
    
    _sessions.Clear();
    _sessionsLoaded = true;
    
    try
    {
      if (!File.Exists(DataFileName))
        return;
      
      var sessionDict = new Dictionary<string, HaggleSession>();
      
      var sessionLines = File.ReadAllLines(DataFileName).Skip(1);
      foreach (var line in sessionLines)
      {
        var parts = line.Split(';');
        if (parts.Length < 16)
          continue;
        
        var session = new HaggleSession
        {
          StartTime = DateTime.Parse(parts[1]),
          EndTime = DateTime.Parse(parts[2]),
          TotalRolls = int.Parse(parts[3]),
          CoinsSpent = int.Parse(parts[4]),
          CoinageValueAtTime = parts.Length > 5 ? float.Parse(parts[5], CultureInfo.InvariantCulture) : 0.5f,
          LesserUsed = parts.Length > 7 ? int.Parse(parts[7]) : int.Parse(parts[5]),
          GreaterUsed = parts.Length > 8 ? int.Parse(parts[8]) : int.Parse(parts[6]),
          GrandUsed = parts.Length > 9 ? int.Parse(parts[9]) : int.Parse(parts[7]),
          ExceptionalUsed = parts.Length > 10 ? int.Parse(parts[10]) : int.Parse(parts[8])
        };
        
        sessionDict[parts[0]] = session;
      }
      
      if (File.Exists(ItemsFileName))
      {
        var itemLines = File.ReadAllLines(ItemsFileName).Skip(1);
        foreach (var line in itemLines)
        {
          var parts = line.Split(';');
          if (parts.Length < 10)
            continue;
          
          var sessionId = parts[0];
          if (!sessionDict.ContainsKey(sessionId))
            continue;
          
          var item = new HaggleSessionItem
          {
            ItemName = parts[1],
            ItemType = parts[2],
            Details = parts[3],
            ChaosValue = float.Parse(parts[4], CultureInfo.InvariantCulture),
            ArtifactCost = float.Parse(parts[5], CultureInfo.InvariantCulture),
            ArtifactType = parts[6],
            ArtifactAmount = parts.Length > 7 ? int.Parse(parts[7]) : 0,
            Amount = parts.Length > 8 ? int.Parse(parts[8]) : int.Parse(parts[7]),
            State = parts.Length > 9 ? Enum.Parse<HaggleItemState>(parts[9]) : Enum.Parse<HaggleItemState>(parts[8]),
            RejectReason = parts.Length > 10 ? parts[10] : (parts.Length > 9 ? parts[9] : "")
          };
          
          sessionDict[sessionId].Items.Add(item);
        }
      }
      
      _sessions = sessionDict.Values.OrderByDescending(x => x.StartTime).ToList();
    }
    catch (Exception ex)
    {
      Log.Error($"Error loading haggle sessions: {ex.Message}");
    }
  }
  
  public static void RenderButton()
  {
    LoadSessions();
    
    ImGui.TextColored(
      new System.Numerics.Vector4(0.7f, 0.7f, 1.0f, 1.0f),
      $"Haggle sessions: {_sessions.Count}"
    );
    
    if (_sessions.Count > 0)
    {
      ImGui.SameLine();
      if (ImGui.Button("Clear All History"))
      {
        ClearHistory();
      }
    }
  }
  
  public static void RenderWindow()
  {
    if (TujenMem.Instance?.Settings?.ShowHaggleHistoryWindow?.Value != true)
      return;
    
    LoadSessions();
    
    ImGui.SetNextWindowSize(new System.Numerics.Vector2(1200, 700), ImGuiCond.FirstUseEver);
    
    var showWindow = TujenMem.Instance.Settings.ShowHaggleHistoryWindow.Value;
    if (ImGui.Begin("Haggle History", ref showWindow))
    {
      TujenMem.Instance.Settings.ShowHaggleHistoryWindow.Value = showWindow;
      
      if (_sessions.Count == 0)
      {
        ImGui.TextColored(
          new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
          "No haggle sessions recorded yet."
        );
        ImGui.End();
        return;
      }
      
      RenderSummary();
      ImGui.Separator();
      RenderSessionsList();
    }
    
    ImGui.End();
  }
  
  private static void RenderSummary()
  {
    var totalSessions = _sessions.Count;
    var totalRolls = _sessions.Sum(x => x.TotalRolls);
    var totalBought = _sessions.Sum(x => x.ItemsBought);
    var totalValue = _sessions.Sum(x => x.TotalChaosValue);
    var totalCost = _sessions.Sum(x => x.TotalCostInChaos);
    var totalProfit = totalValue - totalCost;
    var avgProfitPercent = totalCost > 0 ? (totalProfit / totalCost) * 100 : 0;
    
    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f), "Overall Statistics:");
    ImGui.Text($"Total Sessions: {totalSessions} | Total Rolls: {totalRolls} | Items Bought: {totalBought}");
    
    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.84f, 0.0f, 1.0f), $"Total Value: {totalValue:F2}c");
    ImGui.SameLine();
    ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f), $"| Total Cost: {totalCost:F2}c");
    ImGui.SameLine();
    
    var profitColor = totalProfit > 0 
      ? new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f)
      : new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    ImGui.TextColored(profitColor, $"| Profit: {totalProfit:F2}c ({avgProfitPercent:F1}%)");
  }
  
  private static int _selectedSessionIndex = -1;
  private static string _itemSearchFilter = "";
  
  private static void RenderSessionsList()
  {
    var leftPaneWidth = 400f;
    
    ImGui.BeginChild("SessionsList", new System.Numerics.Vector2(leftPaneWidth, 0), ImGuiChildFlags.Border);
    
    if (ImGui.BeginTable("SessionsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
    {
      ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 120);
      ImGui.TableSetupColumn("Rolls", ImGuiTableColumnFlags.WidthFixed, 50);
      ImGui.TableSetupColumn("Bought", ImGuiTableColumnFlags.WidthFixed, 60);
      ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80);
      ImGui.TableSetupColumn("Profit%", ImGuiTableColumnFlags.WidthFixed, 70);
      ImGui.TableSetupScrollFreeze(0, 1);
      ImGui.TableHeadersRow();
      
      for (int i = 0; i < _sessions.Count; i++)
      {
        var session = _sessions[i];
        ImGui.TableNextRow();
        
        ImGui.TableNextColumn();
        var isSelected = _selectedSessionIndex == i;
        if (ImGui.Selectable($"{session.StartTime:MM-dd HH:mm}", isSelected, ImGuiSelectableFlags.SpanAllColumns))
        {
          _selectedSessionIndex = i;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text(session.TotalRolls.ToString());
        
        ImGui.TableNextColumn();
        ImGui.Text(session.ItemsBought.ToString());
        
        ImGui.TableNextColumn();
        ImGui.TextColored(
          new System.Numerics.Vector4(1.0f, 0.84f, 0.0f, 1.0f),
          $"{session.TotalChaosValue:F0}c"
        );
        
        ImGui.TableNextColumn();
        var profitColor = session.ProfitPercent > 0 
          ? new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f)
          : new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        ImGui.TextColored(profitColor, $"{session.ProfitPercent:F0}%");
      }
      
      ImGui.EndTable();
    }
    
    ImGui.EndChild();
    
    ImGui.SameLine();
    
    ImGui.BeginChild("SessionDetails", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.Border);
    
    if (_selectedSessionIndex >= 0 && _selectedSessionIndex < _sessions.Count)
    {
      RenderSessionDetails(_sessions[_selectedSessionIndex]);
    }
    else
    {
      ImGui.TextColored(
        new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f),
        "Select a session to view details"
      );
    }
    
    ImGui.EndChild();
  }
  
  private static void RenderSessionDetails(HaggleSession session)
  {
    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 1.0f, 0.5f, 1.0f), $"Session: {session.StartTime:yyyy-MM-dd HH:mm:ss}");
    ImGui.Text($"Duration: {(session.EndTime - session.StartTime).TotalMinutes:F1} minutes");
    ImGui.Separator();
    
    ImGui.Text($"Total Rolls: {session.TotalRolls}");
    ImGui.Text($"Coins Spent: {session.CoinsSpent}");
    ImGui.Text($"Exotic Coinage Price: {session.CoinageValueAtTime:F2}c (at time of session)");
    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.5f, 0.0f, 1.0f), $"Reroll Cost: {session.RerollCostInChaos:F2}c");
    
    ImGui.Separator();
    ImGui.Text("Artifacts Used:");
    ImGui.Text($"  Lesser: {session.LesserUsed}");
    ImGui.Text($"  Greater: {session.GreaterUsed}");
    ImGui.Text($"  Grand: {session.GrandUsed}");
    ImGui.Text($"  Exceptional: {session.ExceptionalUsed}");
    
    ImGui.Separator();
    ImGui.Text($"Items Bought: {session.ItemsBought} | Rejected: {session.ItemsRejected} | Too Expensive: {session.ItemsTooExpensive}");
    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.84f, 0.0f, 1.0f), $"Total Value (Bought): {session.TotalChaosValue:F2}c");
    ImGui.SameLine();
    ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f), $"| Artifacts Cost: {session.TotalCostInChaos:F2}c");
    ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1.0f, 1.0f), $"Total Cost (Artifacts + Rerolls): {session.TotalCostWithRerolls:F2}c");
    var profitColor = session.ProfitInChaos > 0 
      ? new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f)
      : new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    ImGui.TextColored(profitColor, $"Net Profit: {session.ProfitInChaos:F2}c ({session.ProfitPercent:F1}%)");
    
    ImGui.Separator();
    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f), "Items:");
    
    ImGui.SetNextItemWidth(300);
    ImGui.InputText("##ItemSearch", ref _itemSearchFilter, 256);
    ImGui.SameLine();
    if (ImGui.Button("Clear"))
    {
      _itemSearchFilter = "";
    }
    
    if (ImGui.BeginTabBar("ItemsTabs"))
    {
      var boughtItems = session.Items.Where(x => x.State == HaggleItemState.Bought).ToList();
      var boughtTotalValue = boughtItems.Sum(x => x.ChaosValue * x.Amount);
      var boughtTotalCost = boughtItems.Sum(x => x.ArtifactCost);
      
      if (ImGui.BeginTabItem($"Bought ({boughtItems.Count})"))
      {
        ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f), $"Total Value: {boughtTotalValue:F2}c | Total Cost: {boughtTotalCost:F2}c | Profit: {(boughtTotalValue - boughtTotalCost):F2}c");
        RenderBoughtItemsTable(boughtItems);
        ImGui.EndTabItem();
      }
      
      if (ImGui.BeginTabItem($"Rejected ({session.ItemsRejected})"))
      {
        RenderItemsTable(session.Items.Where(x => x.State == HaggleItemState.Rejected).ToList());
        ImGui.EndTabItem();
      }
      
      if (ImGui.BeginTabItem($"Too Expensive ({session.ItemsTooExpensive})"))
      {
        RenderItemsTable(session.Items.Where(x => x.State == HaggleItemState.TooExpensive).ToList());
        ImGui.EndTabItem();
      }
      
      if (ImGui.BeginTabItem($"All ({session.Items.Count})"))
      {
        RenderItemsTable(session.Items);
        ImGui.EndTabItem();
      }
      
      ImGui.EndTabBar();
    }
  }
  
  private static void RenderBoughtItemsTable(List<HaggleSessionItem> items)
  {
    if (items.Count == 0)
    {
      ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No items bought");
      return;
    }
    
    var filteredItems = items;
    if (!string.IsNullOrWhiteSpace(_itemSearchFilter))
    {
      var searchLower = _itemSearchFilter.ToLower();
      filteredItems = items.Where(x => 
        x.ItemName.ToLower().Contains(searchLower) ||
        x.ItemType.ToLower().Contains(searchLower) ||
        (x.Details != null && x.Details.ToLower().Contains(searchLower))
      ).ToList();
    }
    
    if (ImGui.BeginTable("BoughtItemsTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
    {
      ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100);
      ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("Stack", ImGuiTableColumnFlags.WidthFixed, 50);
      ImGui.TableSetupColumn("Total Value", ImGuiTableColumnFlags.WidthFixed, 90);
      ImGui.TableSetupColumn("Artifact Cost", ImGuiTableColumnFlags.WidthFixed, 150);
      ImGui.TableSetupScrollFreeze(0, 1);
      ImGui.TableHeadersRow();
      
      foreach (var item in filteredItems)
      {
        ImGui.TableNextRow();
        
        ImGui.TableNextColumn();
        ImGui.Text(item.ItemName);
        
        ImGui.TableNextColumn();
        ImGui.Text(item.ItemType);
        
        ImGui.TableNextColumn();
        if (!string.IsNullOrEmpty(item.Details))
        {
          ImGui.TextWrapped(item.Details);
        }
        else
        {
          ImGui.Text("-");
        }
        
        ImGui.TableNextColumn();
        if (item.Amount > 1)
        {
          ImGui.TextColored(
            new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f),
            $"x{item.Amount}"
          );
        }
        else
        {
          ImGui.Text("-");
        }
        
        ImGui.TableNextColumn();
        var totalValue = item.ChaosValue * item.Amount;
        ImGui.TextColored(
          new System.Numerics.Vector4(1.0f, 0.84f, 0.0f, 1.0f),
          $"{totalValue:F1}c"
        );
        
        ImGui.TableNextColumn();
        var artifactShortName = GetArtifactShortName(item.ArtifactType);
        ImGui.TextColored(
          new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f),
          $"{item.ArtifactAmount} {artifactShortName} ({item.ArtifactCost:F1}c)"
        );
      }
      
      ImGui.EndTable();
    }
  }
  
  private static string GetArtifactShortName(string artifactType)
  {
    if (string.IsNullOrEmpty(artifactType))
      return "";
    
    if (artifactType.Contains("Lesser"))
      return "Lesser";
    if (artifactType.Contains("Greater"))
      return "Greater";
    if (artifactType.Contains("Grand"))
      return "Grand";
    if (artifactType.Contains("Exceptional"))
      return "Exceptional";
    
    return artifactType;
  }
  
  private static void RenderItemsTable(List<HaggleSessionItem> items)
  {
    if (items.Count == 0)
    {
      ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No items");
      return;
    }
    
    var filteredItems = items;
    if (!string.IsNullOrWhiteSpace(_itemSearchFilter))
    {
      var searchLower = _itemSearchFilter.ToLower();
      filteredItems = items.Where(x => 
        x.ItemName.ToLower().Contains(searchLower) ||
        x.ItemType.ToLower().Contains(searchLower) ||
        (x.Details != null && x.Details.ToLower().Contains(searchLower))
      ).ToList();
    }
    
    if (ImGui.BeginTable("ItemsDetailsTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
    {
      ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100);
      ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("Stack", ImGuiTableColumnFlags.WidthFixed, 50);
      ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80);
      ImGui.TableSetupColumn("Cost", ImGuiTableColumnFlags.WidthFixed, 70);
      ImGui.TableSetupColumn("Reason", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupScrollFreeze(0, 1);
      ImGui.TableHeadersRow();
      
      foreach (var item in filteredItems)
      {
        ImGui.TableNextRow();
        
        ImGui.TableNextColumn();
        ImGui.Text(item.ItemName);
        
        ImGui.TableNextColumn();
        ImGui.Text(item.ItemType);
        
        ImGui.TableNextColumn();
        if (!string.IsNullOrEmpty(item.Details))
        {
          ImGui.TextWrapped(item.Details);
        }
        else
        {
          ImGui.Text("-");
        }
        
        ImGui.TableNextColumn();
        if (item.Amount > 1)
        {
          ImGui.TextColored(
            new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f),
            $"x{item.Amount}"
          );
        }
        else
        {
          ImGui.Text("-");
        }
        
        ImGui.TableNextColumn();
        var totalValue = item.ChaosValue * item.Amount;
        ImGui.TextColored(
          new System.Numerics.Vector4(1.0f, 0.84f, 0.0f, 1.0f),
          $"{totalValue:F1}c"
        );
        
        ImGui.TableNextColumn();
        if (item.State == HaggleItemState.Bought)
        {
          ImGui.TextColored(
            new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f),
            $"{item.ArtifactCost:F1}c"
          );
        }
        else
        {
          ImGui.Text("-");
        }
        
        ImGui.TableNextColumn();
        if (!string.IsNullOrEmpty(item.RejectReason))
        {
          ImGui.TextWrapped(item.RejectReason);
        }
        else
        {
          ImGui.Text("-");
        }
      }
      
      ImGui.EndTable();
    }
  }
  
  private static void ClearHistory()
  {
    try
    {
      if (File.Exists(DataFileName))
        File.Delete(DataFileName);
      
      if (File.Exists(ItemsFileName))
        File.Delete(ItemsFileName);
      
      _sessions.Clear();
      _selectedSessionIndex = -1;
      _sessionsLoaded = false;
    }
    catch (Exception ex)
    {
      Log.Error($"Error clearing history: {ex.Message}");
    }
  }
  
  private static string GetItemDetails(HaggleItem item)
  {
    switch (item)
    {
      case HaggleItemGem gem:
        return $"Lvl {gem.Level}, Q{gem.Quality}%{(gem.Corrupted ? ", Corrupted" : "")}";
      
      case HaggleItemClusterJewel cluster:
        return $"iLvl {cluster.ItemLevel}, {cluster.PassiveSkills} Passives";
      
      case HaggleItemMap map:
        return $"T{map.MapTier}{(map.IsUnique ? ", Unique" : "")}{(map.IsInfluenced ? ", Influenced" : "")}";
      
      default:
        return "";
    }
  }
}

