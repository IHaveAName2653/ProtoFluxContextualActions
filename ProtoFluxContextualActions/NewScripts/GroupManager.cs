using System;
using System.Collections.Generic;
using System.Linq;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using Elements.Core;
using static ProtoFluxContextualActions.Patches.ContextualSelectionActionsPatch;
using Elements.Quantity;

namespace ProtoFluxContextualActions.NewScripts;

internal struct ContextItem
{
	internal string name;
	internal colorX? color;
	internal Action onClick;
	internal Uri? uri;
}

internal class GroupManager
{
	// Page Config
	const int MaxPerPage = 8; // Maximum possible items in the context menu, excluding Next/Back buttons
	const bool ShowBackOnAllPages = false;

	// Folder Config
	static readonly Uri FolderIcon = new("resdb:///c8628c05dc2c5a047d90455da53ada83d3d4a2279662efbe156e2147f893f5b0.png");

	// Instance Variables
	readonly Dictionary<string, List<List<MenuItem>>> PagedGroups = [];
	readonly List<MenuItem> RootItems = [];
	readonly Action<MenuItem> onItemClicked;
	readonly ProtoFluxTool currentTool;
	readonly colorX? itemColor;

	internal GroupManager(ProtoFluxTool tool, List<MenuItem> items, colorX? targetColor, Action<MenuItem> onClicked)
	{
		Dictionary<string, List<MenuItem>> GroupedItems = [];
		items.ForEach((item) =>
		{
			if (item.group == "") RootItems.Add(item);
			else if (GroupedItems.TryGetValue(item.group, out List<MenuItem>? list)) list.Add(item);
			else GroupedItems.Add(item.group, [item]);
		});
		PagedGroups = GroupedItems.ToDictionary(kv => kv.Key, kv => kv.Value.SplitToGroups(MaxPerPage));
		onItemClicked = onClicked;
		currentTool = tool;
		itemColor = targetColor;
	}

	internal void RenderGroups()
	{
		RenderRoot();
	}

	void RenderRoot()
	{
		if (currentTool.IsRemoved) return;
		if (PagedGroups.Count + RootItems.Count == 0) return;
		currentTool.StartTask(async () =>
		{
			if (PagedGroups.Count + RootItems.Count != 1) {
				List<ContextItem> currentRootItems = [];
				foreach (var group in PagedGroups)
				{
					currentRootItems.Add(new()
					{
						name=group.Key,
						color=colorX.White,
						onClick=() => RenderFolder(group.Value, 0),
						uri=FolderIcon
					});
				}
				foreach (var item in RootItems)
				{
					colorX targetColor = itemColor ?? item.node.GetTypeColor();
					currentRootItems.Add(new()
					{
						name=item.DisplayName,
						color=targetColor,
						onClick=()=>onItemClicked(item)
					});
				}
				List<List<ContextItem>> pagedRootItems = currentRootItems.SplitToGroups(MaxPerPage);
				RenderRootFolder(pagedRootItems, 0);
			}
			else RenderFolder(PagedGroups.Values.ToList()[0], 0);
		});
	}

	void RenderRootFolder(List<List<ContextItem>> Items, int pageIndex)
	{
		if (currentTool.IsRemoved) return;
		bool showPreviousButton = pageIndex > 0;
		bool showNextButton = pageIndex < Items.Count - 1;
		

		currentTool.StartTask(async () =>
		{
			var menu = await ContextUtils.CreateContextMenu(currentTool);

			if (showPreviousButton)
			{
				menu.AddMenuItem("Previous", colorX.Orange, () => RenderRootFolder(Items, pageIndex - 1));
			}

			foreach (var item in Items[pageIndex])
			{
				menu.AddMenuItem(item.name, item.color, item.onClick, item.uri);
			}


			if (showNextButton)
			{
				menu.AddMenuItem("Next", colorX.Cyan, () => RenderRootFolder(Items, pageIndex + 1));
			}
		});
	}

	void RenderFolder(List<List<MenuItem>> Items, int pageIndex)
	{
		if (currentTool.IsRemoved) return;
		bool showPreviousButton = pageIndex > 0;
		bool showNextButton = pageIndex < Items.Count - 1;
		bool showBackButton = (Items.Count + RootItems.Count) != 1 && (ShowBackOnAllPages || pageIndex == 0);
		

		currentTool.StartTask(async () =>
		{
			var menu = await ContextUtils.CreateContextMenu(currentTool);

			if (showBackButton)
			{
				menu.AddMenuItem("Back", colorX.Red, () => RenderRoot());
			}
			if (showPreviousButton)
			{
				menu.AddMenuItem("Previous", colorX.Orange, () => RenderFolder(Items, pageIndex - 1));
			}

			foreach (var item in Items[pageIndex])
			{
				colorX targetColor = itemColor ?? item.node.GetTypeColor();
				menu.AddMenuItem(item.DisplayName, targetColor, () => onItemClicked(item));
			}


			if (showNextButton)
			{
				menu.AddMenuItem("Next", colorX.Cyan, () => RenderFolder(Items, pageIndex + 1));
			}
		});
	}
}