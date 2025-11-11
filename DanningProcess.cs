using System;
using System.Collections;
using System.Linq;
using ExileCore;
using ExileCore.Shared;
using System.Windows.Forms;
using SharpDX;

namespace TujenMem;

public class DanningProcess
{
  public DanningProcessWindow CurrentWindow = null;

  public DanningProcess()
  {
    Log.Debug("DanningProcess initialized");
  }

  public void InitializeWindow()
  {
    CurrentWindow = new DanningProcessWindow();
  }

  public IEnumerator Run()
  {
    CurrentWindow.ReadItems();
    yield return new WaitTime(0);

    Log.Debug($"Found {CurrentWindow.Items.Count} total items in window");

    // Duyệt qua từng item, hover để lấy thông tin, sau đó quyết định có mua không
    foreach (var item in CurrentWindow.Items)
    {
      if (!CanRun())
      {
        Log.Debug("Cannot continue - out of coins or artifacts. Stopping.");
        yield break;
      }

      // Hover vào item để lấy cost (nếu cần)
      yield return HoverAndReadCost(item);

      // Kiểm tra xem có nên mua item này không
      if (ShouldBuyItem(item))
      {
        Log.Debug($"Buying item: {item.Name} - {item.PriceString}");
        yield return BuyItem(item);
      }
      else
      {
        Log.Debug($"Skipping item: {item.Name} - {item.PriceString}");
      }

      if (Error.IsDisplaying)
      {
        yield break;
      }

      if (!CanRun())
      {
        Log.Debug("Ran out of coins or artifacts during processing. Stopping.");
        yield break;
      }
    }

    Log.Debug("Finished processing all Dannig items");
  }

  private IEnumerator HoverAndReadCost(DanningItem item)
  {
    var haggleWindow = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow;
    if (haggleWindow == null || !haggleWindow.IsVisible || item.ItemIndex < 0)
    {
      yield break;
    }

    if (item.ItemIndex >= haggleWindow.InventoryItems.Count)
    {
      yield break;
    }

    var inventoryItem = haggleWindow.InventoryItems[item.ItemIndex];
    var itemRect = inventoryItem.GetClientRect();
    var position = DanningProcessWindow.GetRandomPositionInRect(itemRect);

    Input.SetCursorPos(position);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);

    // Đọc tooltip
    var tooltip = inventoryItem.Tooltip;
    if (tooltip == null)
    {
      item.PriceString = "N/A (No tooltip)";
      yield break;
    }

    try
    {
      var ttBody = tooltip.GetChildFromIndices(0, 1);
      if (ttBody == null || ttBody.Children.Count == 0)
      {
        item.PriceString = "N/A (No tooltip body)";
        yield break;
      }

      var ttPriceSection = ttBody.GetChildAtIndex(ttBody.Children.Count - 1);
      if (ttPriceSection == null || ttPriceSection.Children.Count < 2)
      {
        item.PriceString = "N/A (No price section)";
        yield break;
      }

      var ttPriceBody = ttPriceSection.GetChildAtIndex(1);
      if (ttPriceBody == null || ttPriceBody.Children.Count < 3)
      {
        item.PriceString = "N/A (No price body)";
        yield break;
      }

      var ttPriceBodyChild = ttPriceBody.GetChildAtIndex(0);
      if (ttPriceBodyChild == null)
      {
        item.PriceString = "N/A (Price element at index 0 not found)";
        yield break;
      }
      
      string priceString = ttPriceBodyChild.Text;
      string cleaned = new string(priceString.Where(char.IsDigit).ToArray()).Trim();
      var ttPrice = 0;

      if (int.TryParse(cleaned, out ttPrice))
      {
        var ttPriceTypeChild = ttPriceBody.GetChildAtIndex(2);
        if (ttPriceTypeChild == null)
        {
          item.PriceString = "N/A (Price type element at index 2 not found)";
          yield break;
        }
        
        var ttPriceType = ttPriceTypeChild.Text;
        item.Price = new HaggleCurrency(ttPriceType, ttPrice);
        item.PriceString = $"{ttPrice} {ttPriceType}";
        Log.Debug($"Read cost for {item.Name}: {item.PriceString}");
      }
      else
      {
        item.PriceString = $"N/A (Parse error: {priceString})";
      }
    }
    catch (Exception ex)
    {
      item.PriceString = $"N/A (Error: {ex.Message})";
      Log.Debug($"Error reading cost for {item.Name}: {ex.Message}");
    }
  }

  private bool ShouldBuyItem(DanningItem item)
  {
    var settings = TujenMem.Instance.Settings.Danning;
    var itemName = item.Name.ToLower();
    var itemPath = item.Path.ToLower();

    // Artifact: cần check cost
    if (IsArtifact(itemName, itemPath))
    {
      if (!IsArtifactFactionEnabled(itemName, itemPath))
      {
        return false;
      }

      // Phải có price mới mua được artifact
      if (item.Price == null || item.PriceString.Contains("N/A"))
      {
        Log.Debug($"Artifact {item.Name} has no valid price");
        return false;
      }

      var priceValue = item.Price.Value;
      var priceType = item.Price.Name.ToLower();

      float maxCost = 0.8f;
      if (priceType.Contains("lesser"))
      {
        maxCost = settings.ArtifactCostSettings.LesserMaxCost.Value;
      }
      else if (priceType.Contains("greater"))
      {
        maxCost = settings.ArtifactCostSettings.GreaterMaxCost.Value;
      }
      else if (priceType.Contains("grand"))
      {
        maxCost = settings.ArtifactCostSettings.GrandMaxCost.Value;
      }
      else if (priceType.Contains("exceptional"))
      {
        maxCost = settings.ArtifactCostSettings.ExceptionalMaxCost.Value;
      }

      var normalizedCost = priceValue / 10.0f;
      var shouldBuy = normalizedCost <= maxCost;
      Log.Debug($"Artifact {item.Name}: cost={normalizedCost:F2}, maxCost={maxCost:F2}, shouldBuy={shouldBuy}");
      return shouldBuy;
    }

    // Reroll items: mua ngay, không cần check cost
    if (IsRerollItem(itemName, itemPath))
    {
      if (itemName.Contains("exotic") || itemPath.Contains("exotic"))
      {
        return settings.BuyExoticCoinage.Value;
      }
      if (itemName.Contains("astragali") || itemPath.Contains("astragali"))
      {
        return settings.BuyAstragali.Value;
      }
      if (itemName.Contains("scrap") || itemPath.Contains("scrap"))
      {
        return settings.BuyScrapMetal.Value;
      }
    }

    // Logbooks: mua ngay, không cần check cost
    if (IsLogbook(itemName, itemPath))
    {
      if (itemName.Contains("black scythe") || itemPath.Contains("blackscythe"))
      {
        return settings.BuyBlackScytheLogbook.Value;
      }
      if (itemName.Contains("knight") || itemPath.Contains("knight"))
      {
        return settings.BuyKnightOfTheSunLogbook.Value;
      }
    }

    return false;
  }

  private bool IsArtifact(string itemName, string itemPath)
  {
    return (itemName.Contains("black scythe artifact") || itemPath.Contains("blackscytheartifact"))
        || (itemName.Contains("order artifact") || itemPath.Contains("orderartifact"))
        || (itemName.Contains("broken circle artifact") || itemPath.Contains("brokencircleartifact"));
  }

  private bool IsArtifactFactionEnabled(string itemName, string itemPath)
  {
    var settings = TujenMem.Instance.Settings.Danning;
    
    if (itemName.Contains("black scythe") || itemPath.Contains("blackscythe"))
    {
      return settings.BuyBlackScytheArtifacts.Value;
    }
    if (itemName.Contains("order") || itemPath.Contains("order"))
    {
      return settings.BuyOrderArtifacts.Value;
    }
    if (itemName.Contains("broken circle") || itemPath.Contains("brokencircle"))
    {
      return settings.BuyBrokenCircleArtifacts.Value;
    }
    
    return false;
  }

  private bool IsRerollItem(string itemName, string itemPath)
  {
    return itemName.Contains("exotic") || itemName.Contains("astragali") || itemName.Contains("scrap")
        || itemPath.Contains("exotic") || itemPath.Contains("astragali") || itemPath.Contains("scrap");
  }

  private bool IsLogbook(string itemName, string itemPath)
  {
    return (itemName.Contains("logbook") || itemPath.Contains("logbook"))
        && ((itemName.Contains("black scythe") || itemPath.Contains("blackscythe"))
        || (itemName.Contains("knight") || itemPath.Contains("knight")));
  }

  private IEnumerator BuyItem(DanningItem item)
  {
    var haggleWindow = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow;
    if (haggleWindow == null || !haggleWindow.IsVisible || item.ItemIndex < 0)
    {
      Log.Debug($"Cannot buy item {item.Name}: window not visible or invalid index");
      yield break;
    }

    if (item.ItemIndex >= haggleWindow.InventoryItems.Count)
    {
      Log.Debug($"Cannot buy item {item.Name}: index {item.ItemIndex} out of range");
      yield break;
    }

    var inventoryItem = haggleWindow.InventoryItems[item.ItemIndex];
    var itemRect = inventoryItem.GetClientRect();
    var position = DanningProcessWindow.GetRandomPositionInRect(itemRect);

    // Cursor đã ở đúng vị trí từ HoverAndReadCost, nhưng di chuyển lại cho chắc
    Input.SetCursorPos(position);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);

    // Ctrl+Click để mua
    Input.KeyDown(Keys.ControlKey);
    yield return new WaitTime(50);
    Input.Click(MouseButtons.Left);
    yield return new WaitTime(50);
    Input.KeyUp(Keys.ControlKey);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);

    Log.Debug($"✓ Bought: {item.Name} - {item.PriceString}");
  }

  public bool CanRun()
  {
    var canRun = HaggleStock.Coins > 0
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableLesser || HaggleStock.Lesser > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGreater || HaggleStock.Greater > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGrand || HaggleStock.Grand > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableExceptional || HaggleStock.Exceptional > 300);

    Log.Debug($"CanRun: {canRun} - Coins: {HaggleStock.Coins} - Lesser: {HaggleStock.Lesser} - Greater: {HaggleStock.Greater} - Grand: {HaggleStock.Grand} - Exceptional: {HaggleStock.Exceptional}");

    return canRun;
  }
}

