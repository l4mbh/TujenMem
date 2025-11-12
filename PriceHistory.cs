using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImGuiNET;

namespace TujenMem;

public static class PriceHistory
{
  private static string _searchFilter = "";
  private static string _typeFilter = "";
  
  public static void RenderButton()
  {
    var itemCount = Ninja.Items.Sum(x => x.Value.Count);
    ImGui.TextColored(
      new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f),
      $"Downloaded prices: {itemCount} items"
    );
  }
  
  public static void RenderWindow()
  {
    if (TujenMem.Instance?.Settings?.ShowPriceHistoryWindow?.Value != true)
      return;
    
    ImGui.SetNextWindowSize(new System.Numerics.Vector2(1000, 600), ImGuiCond.FirstUseEver);
    
    var showWindow = TujenMem.Instance.Settings.ShowPriceHistoryWindow.Value;
    if (ImGui.Begin("Downloaded Prices from poe.ninja", ref showWindow))
    {
      TujenMem.Instance.Settings.ShowPriceHistoryWindow.Value = showWindow;
      
      if (Ninja.Items.Count == 0)
      {
        ImGui.TextColored(
          new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
          "No data. Click 'Download Data' or 'Download Exchange Data' in Ninja Data settings."
        );
        ImGui.End();
        return;
      }
      
      ImGui.InputTextWithHint("##search", "Search item name...", ref _searchFilter, 256);
      ImGui.SameLine();
      ImGui.InputTextWithHint("##typefilter", "Filter by type...", ref _typeFilter, 256);
      ImGui.SameLine();
      if (ImGui.Button("Clear Filters"))
      {
        _searchFilter = "";
        _typeFilter = "";
      }
      
      ImGui.Separator();
      
      var allItems = new List<(string Name, string Type, string Details, float ChaosValue, bool IsCustomPrice)>();
      
      foreach (var kvp in Ninja.Items)
      {
        foreach (var ninjaItem in kvp.Value)
        {
          if (ninjaItem.ChaosValue <= 0)
            continue;
          
          var itemName = ninjaItem.Name;
          var itemType = GetItemType(ninjaItem);
          var details = GetItemDetails(ninjaItem);
          var isCustom = IsCustomPrice(itemName);
          
          if (!string.IsNullOrEmpty(_searchFilter) && 
              !itemName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            continue;
          
          if (!string.IsNullOrEmpty(_typeFilter) && 
              !itemType.Contains(_typeFilter, StringComparison.OrdinalIgnoreCase))
            continue;
          
          allItems.Add((itemName, itemType, details, ninjaItem.ChaosValue, isCustom));
        }
      }
      
      ImGui.Text($"Showing {allItems.Count} items");
      ImGui.Separator();
      
      if (ImGui.BeginTable("NinjaPricesTable", 5, 
          ImGuiTableFlags.Borders | 
          ImGuiTableFlags.RowBg | 
          ImGuiTableFlags.ScrollY | 
          ImGuiTableFlags.Resizable | 
          ImGuiTableFlags.Sortable))
      {
        ImGui.TableSetupColumn("Item Name", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Chaos Value", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();
        
        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.SpecsDirty)
        {
          var spec = sortSpecs.Specs;
          var ascending = spec.SortDirection == ImGuiSortDirection.Ascending;
          
          allItems = spec.ColumnIndex switch
          {
            0 => ascending ? allItems.OrderBy(x => x.Name).ToList() : allItems.OrderByDescending(x => x.Name).ToList(),
            1 => ascending ? allItems.OrderBy(x => x.Type).ToList() : allItems.OrderByDescending(x => x.Type).ToList(),
            3 => ascending ? allItems.OrderBy(x => x.ChaosValue).ToList() : allItems.OrderByDescending(x => x.ChaosValue).ToList(),
            4 => ascending ? allItems.OrderBy(x => x.IsCustomPrice).ToList() : allItems.OrderByDescending(x => x.IsCustomPrice).ToList(),
            _ => allItems
          };
          
          sortSpecs.SpecsDirty = false;
        }
        
        foreach (var item in allItems)
        {
          ImGui.TableNextRow();
          
          ImGui.TableNextColumn();
          ImGui.Text(item.Name);
          
          ImGui.TableNextColumn();
          ImGui.Text(item.Type);
          
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
          var priceColor = item.IsCustomPrice 
            ? new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f)
            : new System.Numerics.Vector4(1.0f, 0.84f, 0.0f, 1.0f);
          ImGui.TextColored(priceColor, item.ChaosValue.ToString("F2", CultureInfo.InvariantCulture));
          
          ImGui.TableNextColumn();
          if (item.IsCustomPrice)
          {
            ImGui.TextColored(
              new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f),
              "Custom"
            );
            if (ImGui.IsItemHovered())
            {
              var customInfo = GetCustomPriceInfo(item.Name);
              if (!string.IsNullOrEmpty(customInfo))
              {
                ImGui.SetTooltip(customInfo);
              }
            }
          }
          else
          {
            ImGui.TextColored(
              new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f),
              "poe.ninja"
            );
          }
        }
        
        ImGui.EndTable();
      }
    }
    
    ImGui.End();
  }
  
  private static bool IsCustomPrice(string itemName)
  {
    if (TujenMem.Instance?.Settings?.CustomPrices == null)
      return false;
    
    return TujenMem.Instance.Settings.CustomPrices.Any(cp => cp.Item1 == itemName);
  }
  
  private static string GetCustomPriceInfo(string itemName)
  {
    if (TujenMem.Instance?.Settings?.CustomPrices == null)
      return "";
    
    var customPrice = TujenMem.Instance.Settings.CustomPrices.FirstOrDefault(cp => cp.Item1 == itemName);
    if (customPrice.Item1 == null)
      return "";
    
    if (customPrice.Item2 != null)
    {
      return $"Fixed value: {customPrice.Item2}c";
    }
    
    if (!string.IsNullOrEmpty(customPrice.Item3))
    {
      return $"Expression: {customPrice.Item3}";
    }
    
    return "Custom price";
  }
  
  private static string GetItemType(NinjaItem item)
  {
    return item switch
    {
      NinjaItemMap => "Map",
      NinjaItemGem => "Skill Gem",
      NinjaItemClusterJewel => "Cluster Jewel",
      _ => "Item"
    };
  }
  
  private static string GetItemDetails(NinjaItem item)
  {
    switch (item)
    {
      case NinjaItemGem gem:
        return $"Lvl {gem.Level}, Q{gem.Quality}%{(gem.Corrupted ? ", Corrupted" : "")}{(gem.SpecialSupport ? ", Special" : "")}";
      
      case NinjaItemClusterJewel cluster:
        return $"iLvl {cluster.ItemLevel}, {cluster.PassiveSkills} Passives, {cluster.BaseType}";
      
      case NinjaItemMap map:
        return $"T{map.Tier}{(map.Unique ? ", Unique" : "")}";
      
      default:
        return "";
    }
  }
}

