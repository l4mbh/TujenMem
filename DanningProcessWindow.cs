using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements.ExpeditionElements;
using System.Collections;
using System;
using System.Windows.Forms;
using System.Linq;
using SharpDX;

namespace TujenMem;

public class DanningProcessWindow
{
  public List<DanningItem> Items { get; set; } = new();

  private static readonly Random _random = new Random();

  public DanningProcessWindow()
  {
  }

  public static Vector2 GetRandomPositionInRect(RectangleF rect, float paddingPercent = 0.2f)
  {
    var paddingX = rect.Width * paddingPercent;
    var paddingY = rect.Height * paddingPercent;
    
    var minX = rect.X + paddingX;
    var maxX = rect.X + rect.Width - paddingX;
    var minY = rect.Y + paddingY;
    var maxY = rect.Y + rect.Height - paddingY;
    
    var randomX = (float)(_random.NextDouble() * (maxX - minX) + minX);
    var randomY = (float)(_random.NextDouble() * (maxY - minY) + minY);
    
    return new Vector2(randomX, randomY);
  }

  public string WindowStatus { get; set; } = "Unknown";
  public int ChildrenCount { get; set; } = 0;
  public string DebugInfo { get; set; } = "";

  public bool IsWindowOpen()
  {
    try
    {
      var ingameUi = TujenMem.Instance.GameController.IngameState.IngameUi;
      WindowStatus = "Checking...";
      DebugInfo = "";

      var haggleWindow = ingameUi.HaggleWindow;
      
      if (haggleWindow == null)
      {
        WindowStatus = "HaggleWindow is null";
        return false;
      }

      if (!haggleWindow.IsVisible)
      {
        WindowStatus = "HaggleWindow is not visible";
        return false;
      }

      var inventoryItemsCount = haggleWindow.InventoryItems?.Count ?? 0;
      ChildrenCount = inventoryItemsCount;
      DebugInfo = $"HaggleWindow.IsVisible: {haggleWindow.IsVisible}\n";
      DebugInfo += $"HaggleWindow.InventoryItems.Count: {inventoryItemsCount}\n";
      
      // Dannig items bắt đầu từ index 12, cần > 12 items
      if (inventoryItemsCount > 12)
      {
        WindowStatus = $"HaggleWindow visible - {inventoryItemsCount - 12} Dannig items available (from index 12)";
        DebugInfo += $"Window is open with {inventoryItemsCount - 12} Dannig items\n";
        return true;
      }

      WindowStatus = $"HaggleWindow visible but not enough items (need > 12, found {inventoryItemsCount})";
      DebugInfo += $"Not enough items for Dannig\n";
      return false;
    }
    catch (Exception ex)
    {
      WindowStatus = $"Error: {ex.Message}";
      DebugInfo = $"Exception: {ex.Message}\n{ex.StackTrace}";
      Log.Debug($"Error checking Dannig window: {ex.Message}");
      return false;
    }
  }

  public void ReadItems()
  {
    Log.Debug("Reading available items from Dannig window (skipping first 12 items)");
    Items.Clear();

    try
    {
      var haggleWindow = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow;
      if (haggleWindow == null || !haggleWindow.IsVisible || haggleWindow.InventoryItems == null)
      {
        Log.Debug("HaggleWindow is not available or not visible");
        return;
      }

      var inventoryItems = haggleWindow.InventoryItems;
      var totalItems = inventoryItems.Count;
      
      Log.Debug($"HaggleWindow.InventoryItems.Count: {totalItems}");
      
      if (totalItems <= 12)
      {
        Log.Debug($"Not enough items (need > 12, found {totalItems})");
        return;
      }

      // BỎ QUA 12 items đầu tiên (index 0-11), chỉ đọc từ index 12 trở đi
      for (int i = 12; i < totalItems; i++)
      {
        try
        {
          var inventoryItem = inventoryItems[i];
          if (inventoryItem != null)
          {
            ProcessInventoryItem(inventoryItem, i);
          }
        }
        catch (Exception ex)
        {
          Log.Debug($"Error processing item at index {i}: {ex.Message}");
        }
      }

      Log.Debug($"Finished reading items from Dannig window. Total items: {Items.Count}");
    }
    catch (Exception ex)
    {
      Log.Debug($"Error reading from HaggleWindow: {ex.Message}");
    }
  }

  private void ProcessInventoryItem(NormalInventoryItem inventoryItem, int itemIndex)
  {
    try
    {
      var baseItem = TujenMem.Instance.GameController.Files.BaseItemTypes.Translate(inventoryItem.Item.Path);
      var stack = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Stack>();
      var address = inventoryItem.Address;
      var position = inventoryItem.GetClientRect().Center;

      var danningItem = new DanningItem
      {
        Address = address,
        Position = position,
        Name = baseItem?.BaseName ?? "Unknown",
        Type = baseItem?.ClassName ?? "Unknown",
        Amount = stack?.Size ?? 1,
        Path = inventoryItem.Item.Path,
        ItemIndex = itemIndex
      };

      ReadItemCost(danningItem, itemIndex);
      
      Items.Add(danningItem);
    }
    catch (Exception ex)
    {
      Log.Debug($"Error processing inventory item: {ex.Message}");
    }
  }

  private void ReadItemCost(DanningItem item, int itemIndex)
  {
    try
    {
      var haggleWindow = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow;
      if (haggleWindow == null || haggleWindow.InventoryItems == null || itemIndex >= haggleWindow.InventoryItems.Count)
      {
        item.PriceString = "N/A (Invalid index)";
        return;
      }

      var inventoryItem = haggleWindow.InventoryItems[itemIndex];
      // Không hover trong ReadItems để tránh chuột di chuyển liên tục
      // Tooltip có thể không có nếu chưa hover, sẽ ghi N/A

      var tooltip = inventoryItem.Tooltip;
      
      if (tooltip == null)
      {
        item.PriceString = "N/A (No tooltip)";
        return;
      }

      try
      {
        var ttBody = tooltip.GetChildFromIndices(0, 1);
        if (ttBody == null || ttBody.Children.Count == 0)
        {
          item.PriceString = "N/A (No tooltip body)";
          return;
        }

        var ttPriceSection = ttBody.GetChildAtIndex(ttBody.Children.Count - 1);
        if (ttPriceSection == null || ttPriceSection.Children.Count < 2)
        {
          item.PriceString = "N/A (No price section)";
          return;
        }

        var ttPriceBody = ttPriceSection.GetChildAtIndex(1);
        if (ttPriceBody == null || ttPriceBody.Children.Count < 3)
        {
          item.PriceString = "N/A (No price body)";
          return;
        }

        var ttPriceBodyChild = ttPriceBody.GetChildAtIndex(0);
        if (ttPriceBodyChild == null)
        {
          item.PriceString = "N/A (Price element at index 0 not found)";
          return;
        }
        
        string priceString = ttPriceBodyChild.Text;
        string cleaned = new string(priceString.Where(char.IsDigit).ToArray()).Trim();
        var ttPrice = 0;
        
        try
        {
          ttPrice = int.Parse(cleaned);
        }
        catch
        {
          item.PriceString = $"N/A (Parse error: {priceString})";
          return;
        }

        var ttPriceTypeChild = ttPriceBody.GetChildAtIndex(2);
        if (ttPriceTypeChild == null)
        {
          item.PriceString = "N/A (Price type element at index 2 not found)";
          return;
        }
        
        var ttPriceType = ttPriceTypeChild.Text;
        item.Price = new HaggleCurrency(ttPriceType, ttPrice);
        item.PriceString = $"{ttPrice} {ttPriceType}";
      }
      catch (Exception ex)
      {
        item.PriceString = $"N/A (Error: {ex.Message})";
        Log.Debug($"Error reading cost for item {item.Name} at index {itemIndex}: {ex.Message}");
      }
    }
    catch (Exception ex)
    {
      item.PriceString = $"N/A (Exception: {ex.Message})";
      Log.Debug($"Error reading cost for item at index {itemIndex}: {ex.Message}");
    }
  }
}

public class DanningItem
{
  public long Address { get; set; }
  public Vector2 Position { get; set; }
  public string Name { get; set; }
  public string Type { get; set; }
  public int Amount { get; set; }
  public string Path { get; set; }
  public HaggleCurrency Price { get; set; }
  public string PriceString { get; set; } = "N/A";
  public int ItemIndex { get; set; } = -1;
}

