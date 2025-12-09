using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using ProtoFlux.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static ProtoFluxContextualActions.Patches.ContextualSelectionActionsPatch;


namespace ProtoFluxContextualActions.Utils;

public interface IGroup
{
  public string? GetGroup();
}
public interface IName
{
  public string? GetDisplayName();
}
public interface IMenuNode
{
  public Type? GetNodeType();
}
public interface IPageItems : IGroup, IName, IMenuNode { }


internal class Pager<T> where T : IPageItems
{
  internal const int MAX_PER_PAGE = 8;

  internal static List<List<T2>> Split<T2>(IList<T2> source)
  {
    return source
      .Select((x, i) => new { Index = i, Value = x })
      .GroupBy(x => x.Index / MAX_PER_PAGE)
      .Select(x => x.Select(v => v.Value).ToList())
      .ToList();
  }

  internal Dictionary<string, List<List<T>>> sortedItems = [];
  internal colorX? itemColor;
  internal ProtoFluxElementProxy? proxy;
  internal Action<ProtoFluxTool, ProtoFluxElementProxy, T>? menuButtonSetup;
  internal void InitPagedItems(
    List<T> Items,
    colorX? color,
    ProtoFluxElementProxy elementProxy,
    Action<ProtoFluxTool, ProtoFluxElementProxy, T> onMenuButtonPressed)
  {
    itemColor = color;
    proxy = elementProxy;
    menuButtonSetup = onMenuButtonPressed;

    if (proxy == null) return;
    if (menuButtonSetup == null) return;

    Dictionary<string, List<T>> halfSortedItems = [];
    foreach (T item in Items)
    {
      string groupName = "Unsorted";
      string? itemGroup = item.GetGroup();
      if (itemGroup != null && !string.IsNullOrEmpty(itemGroup)) groupName = itemGroup;
      if (halfSortedItems.TryGetValue(groupName, out var list))
      {
        list.Add(item);
      }
      else
      {
        halfSortedItems.Add(groupName, [item]);
      }
    }
    foreach (var halfSort in halfSortedItems)
    {
      string groupName = halfSort.Key;
      List<T> items = halfSort.Value;
      List<List<T>> pagedItems = Split(items);
      sortedItems.Add(groupName, pagedItems);
    }
  }

  internal void CreateGroups(ProtoFluxTool tool, ContextMenu menu, colorX color, PageRootData rootData, string? insideSubFolder = null)
  {
    if (sortedItems.Count == 0) return;
    if (insideSubFolder != null)
    {
      menu.AddMenuFolder(insideSubFolder, color, () =>
      {
        foreach (var group in sortedItems)
        {
          if (group.Value.Count == 0) continue;
          AddSubfolder(
            tool,
            menu,
            group.Key,
            color,
            rootData
          );
        }
      });
    }
    foreach (var group in sortedItems)
    {
      if (group.Value.Count == 0) continue;
      AddSubfolder(
        tool,
        menu,
        group.Key,
        color,
        rootData
      );
    }
  }

  private async void AddSubfolder(
  ProtoFluxTool tool,
  ContextMenu menu,
  string folderName,
  colorX color,
  PageRootData rootData)
  {
    menu.AddMenuFolder(folderName, color, () =>
    {
      RebuildPagedMenu(tool, itemColor, sortedItems[folderName], 0, rootData);
    });
  }

  private void RebuildPagedMenu(
        ProtoFluxTool tool,
        colorX? itemColor,
        List<List<T>> PagedItems,
        int page,
        PageRootData rootData)
  {
    tool.StartTask(async () =>
    {
      var newMenu = await ContextHelper.CreateContext(tool);
      if (page == 0)
      {
        var label = (LocaleString)"Back";
        var menuItem = newMenu.AddItem(in label, (Uri?)null, colorX.Red);
        menuItem.Button.LocalPressed += (button, data) =>
        {
          tool.LocalUser.CloseContextMenu(tool);
          CreateRootItems(tool, rootData);
        };
      }
      if (page > 0)
      {
        var label = (LocaleString)$"Previous Page<size=25%>\n\n</size><size=75%>({page}/{PagedItems.Count})</size>";
        var menuItem = newMenu.AddItem(in label, (Uri?)null, colorX.Orange);
        menuItem.Button.LocalPressed += (button, data) =>
        {
          tool.LocalUser.CloseContextMenu(tool);
          RebuildPagedMenu(tool, itemColor, PagedItems, page - 1, rootData);
        };
      }
      foreach (var item in PagedItems[page])
      {
        if (proxy == null) return;
        colorX targetColor = itemColor ?? item.GetNodeType()?.GetTypeColor() ?? colorX.White;
        AddMenuItem(tool, proxy, newMenu, targetColor, item);
      }
      if (PagedItems.Count - 1 > page)
      {
        var label = (LocaleString)$"Next Page<size=25%>\n\n</size><size=75%>({page + 2}/{PagedItems.Count})</size>";
        var menuItem = newMenu.AddItem(in label, (Uri?)null, colorX.Cyan);
        menuItem.Button.LocalPressed += (button, data) =>
        {
          tool.LocalUser.CloseContextMenu(tool);
          RebuildPagedMenu(tool, itemColor, PagedItems, page + 1, rootData);
        };
      }
    });
  }

  private void AddMenuItem(ProtoFluxTool __instance, ProtoFluxElementProxy proxy, ContextMenu menu, colorX color, T item)
  {
    if (menuButtonSetup == null) return; // not that this should happen, as we already do nullchecks
    var label = (LocaleString)item.GetDisplayName();
    var menuItem = menu.AddItem(in label, (Uri?)null, color);
    menuItem.Button.LocalPressed += (button, data) =>
    {
      menuButtonSetup(__instance, proxy, item);
    };
  }
}