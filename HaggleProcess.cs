using System;
using System.Collections;
using System.Linq;
using ExileCore.Shared;

namespace TujenMem;

public class HaggleProcess
{
  public HaggleStock Stock;

  public HaggleProcess()
  {
    Log.Debug($"HaggleProcess initialized with {HaggleStock.Coins} coins.");
  }

  public HaggleProcessWindow CurrentWindow = null;

  public void InitializeWindow()
  {
    CurrentWindow = new HaggleProcessWindow();
  }

  public IEnumerator Run()
  {
    CurrentWindow.ReadItems();
    yield return new WaitTime(0);
    CurrentWindow.ApplyMappingToItems();
    yield return new WaitTime(0);
    CurrentWindow.FilterItems();
    yield return new WaitTime(0);

    var unpricedItems = CurrentWindow.Items.Where(x => x.State == HaggleItemState.Unpriced).ToList();
    var totalItems = unpricedItems.Count;
    
    Log.Debug($"Processing {totalItems} items one by one");

    for (var i = 0; i < unpricedItems.Count; i++)
    {
      if (!CanRun())
      {
        Log.Debug("Cannot continue - out of coins or artifacts. Stopping to empty inventory.");
        yield break;
      }

      var item = unpricedItems[i];
      var itemIndex = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems
        .ToList()
        .FindIndex(x => x.Address == item.Address);
      
      if (itemIndex == -1)
      {
        Log.Debug($"Item {item.Name} not found in inventory, skipping");
        continue;
      }

      var isLastItem = (i == unpricedItems.Count - 1);
      yield return CurrentWindow.ProcessItem(item, itemIndex, isLastItem);
      
      if (Error.IsDisplaying)
      {
        yield break;
      }

      if (!CanRun())
      {
        Log.Debug("Ran out of coins or artifacts during processing. Stopping to empty inventory.");
        yield break;
      }
    }

    Log.Debug("Finished processing all items");
  }


  public bool CanRun()
  {
    var canRun = HaggleStock.Coins > 0
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableLesser || HaggleStock.Lesser > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGreater || HaggleStock.Greater > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGrand || HaggleStock.Grand > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableExceptional || HaggleStock.Exceptional > 300);
    ;

    Log.Debug($"CanRun: {canRun} - Coins: {HaggleStock.Coins} - Lesser: {HaggleStock.Lesser} - Greater: {HaggleStock.Greater} - Grand: {HaggleStock.Grand} - Exceptional: {HaggleStock.Exceptional}");

    return canRun;
  }
}