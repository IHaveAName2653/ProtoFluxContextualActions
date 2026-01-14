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
}

internal class GroupManager
{
	const int MaxPerPage = 12; // Maximum possible items in the context menu, excluding Next/Back buttons

	readonly Dictionary<string, List<List<MenuItem>>> PagedGroups = [];
	readonly List<MenuItem> RootItems = [];
	readonly Action<MenuItem> onItemClicked;
	readonly ProtoFluxTool currentTool;

	internal GroupManager(List<MenuItem> items, Action<MenuItem> onClicked, ProtoFluxTool tool)
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
						onClick=() => RenderFolder(group.Value, 0)
					});
				}
				foreach (var item in RootItems)
				{
					colorX targetColor = item.node?.GetTypeColor() ?? colorX.White;
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
				menu.AddItem("Previous", colorX.Orange, () => RenderRootFolder(Items, pageIndex - 1));
			}

			foreach (var item in Items[pageIndex])
			{
				menu.AddItem(item.name, item.color, item.onClick);
			}


			if (showNextButton)
			{
				menu.AddItem("Next", colorX.Cyan, () => RenderRootFolder(Items, pageIndex + 1));
			}
		});
	}

	void RenderFolder(List<List<MenuItem>> Items, int pageIndex)
	{
		if (currentTool.IsRemoved) return;
		bool showPreviousButton = pageIndex > 0;
		bool showNextButton = pageIndex < Items.Count - 1;
		bool showBackButton = Items.Count != 1;
		

		currentTool.StartTask(async () =>
		{
			var menu = await ContextUtils.CreateContextMenu(currentTool);

			if (showBackButton)
			{
				menu.AddItem("Back", colorX.Red, () => RenderRoot());
			}
			if (showPreviousButton)
			{
				menu.AddItem("Previous", colorX.Orange, () => RenderFolder(Items, pageIndex - 1));
			}

			foreach (var item in Items[pageIndex])
			{
				colorX targetColor = item.node?.GetTypeColor() ?? colorX.White;
				menu.AddItem(item.DisplayName, targetColor, () => onItemClicked(item));
			}


			if (showNextButton)
			{
				menu.AddItem("Next", colorX.Cyan, () => RenderFolder(Items, pageIndex + 1));
			}
		});
	}
}