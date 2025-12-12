using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.Undo;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots;
using ProtoFluxContextualActions.Attributes;
using ProtoFluxContextualActions.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace ProtoFluxContextualActions;

public static class FluxRecipeConfig
{
  public static List<FluxRecipe> FluxRecipes = [];

  static readonly string configPath = Path.Combine(Directory.GetCurrentDirectory(), "rml_config", "__PFCA-Recipes.json");

  public static void OnInit()
  {
    FluxRecipes = [];
    ReadFromConfig();
  }

  public static void OnAddItem(FluxRecipe recipe)
  {
    int possibleIndex = FluxRecipes.FindIndex(el => el.RecipeName == recipe.RecipeName);
    if (possibleIndex != -1) FluxRecipes[possibleIndex] = recipe;
    else FluxRecipes.Add(recipe);
    WriteIntoConfig();
  }

  public static void OnRemoveItem(string ItemName)
  {
    int index = FluxRecipes.FindIndex(recipe => recipe.RecipeName.Equals(ItemName, StringComparison.InvariantCultureIgnoreCase));
    if (index == -1) return;
    FluxRecipes.RemoveAt(index);
    WriteIntoConfig();
  }
  public static void OnRemoveItem(FluxRecipe recipe)
  {
    FluxRecipes.Remove(recipe);
    WriteIntoConfig();
  }

  public static void LoadFromString(string content, bool WriteFile = true)
  {
    FluxRecipes = JsonConvert.DeserializeObject<List<FluxRecipe>>(content) ?? [];
    if (WriteFile) WriteIntoConfig();
  }
  public static void LoadSingleString(string content)
  {
    FluxRecipe obj = JsonConvert.DeserializeObject<FluxRecipe>(content);
    OnAddItem(obj);
  }
  public static void LoadMultiString(string content)
  {
    List<FluxRecipe>? objs = JsonConvert.DeserializeObject<List<FluxRecipe>>(content);
    if (objs == null) return;
    objs.ForEach(OnAddItem);
  }
  public static string StringFromData()
  {
    return JsonConvert.SerializeObject(FluxRecipes);
  }
  public static string GetStringFor(string target)
  {
    List<FluxRecipe> matches = FluxRecipes.FindAll(cur => cur.RecipeName == target);
    if (matches.Count == 0) return "";
    return JsonConvert.SerializeObject(matches[0]);
  }

  public static void WriteIntoConfig()
  {
    if (!File.Exists(configPath))
    {
      File.Create(configPath).Close();
    }
    string content = StringFromData();
    File.WriteAllText(configPath, content, Encoding.Unicode);
  }

  public static void ReadFromConfig()
  {
    if (!File.Exists(configPath))
    {
      File.Create(configPath).Close();
    }
    else
    {
      string content = File.ReadAllText(configPath, Encoding.Unicode);
      LoadFromString(content, false);
    }
  }

  public struct PartialMenuItem(string Name, FluxRecipe recipe, Action<ProtoFluxTool, ProtoFluxElementProxy, FluxRecipe> onMenuPress) : IPageItems
  {
    public string DisplayName = Name;

    public FluxRecipe recipe = recipe;

    public Action<ProtoFluxTool, ProtoFluxElementProxy, FluxRecipe> onMenuPress = onMenuPress;

    public string? GetDisplayName() => DisplayName;
    public string? GetGroup() => recipe.group ?? "Ungrouped";
    public Type? GetNodeType() => recipe.NodeDefinitions[0].NodeType;
  }

  public static IEnumerable<PartialMenuItem> GetItems(ProtoFluxTool tool, ProtoFluxElementProxy proxy)
  {
    bool IsProxyOutput = proxy is ProtoFluxOutputProxy outputProxy;
    bool IsProxyInput = proxy is ProtoFluxInputProxy inputProxy;
    if (!IsProxyOutput && !IsProxyInput)
    {
      yield break;
    }

    List<FluxRecipe> ValidDirectionItems = FluxRecipes.FindAll(recipe => recipe.IsOutputProxy == IsProxyOutput);

    Type? proxyType = null;
    if (IsProxyOutput)
    {
      proxyType = ((ProtoFluxOutputProxy)proxy).OutputType.Value;
    }
    if (IsProxyInput)
    {
      proxyType = ((ProtoFluxInputProxy)proxy).InputType.Value;
    }

    List<FluxRecipe> ValidTypeItems = ValidDirectionItems.FindAll(recipe => recipe.AllowedProxyTypes.Exists(t => t == proxyType));

    foreach (FluxRecipe recipe in ValidTypeItems)
    {
      yield return new PartialMenuItem(
              recipe.RecipeName,
              recipe,
              ConstructFluxRecipe
      );
    }
  }
  public static void ConstructFluxRecipe(ProtoFluxTool tool, ProtoFluxElementProxy proxy, FluxRecipe target) => ConstructFluxRecipe(tool, proxy, target, false);

  public static void ConstructFluxRecipe(ProtoFluxTool tool, ProtoFluxElementProxy proxy, FluxRecipe target, bool DontConnectOut = false, Slot Into = null)
  {
    if (target.NodeDefinitions.Any(def => def.IsTypeNull)) return;
    tool.StartTask(async () =>
    {
      List<Type> spawningTypes = target.NodeDefinitions.Select(def => def.NodeType).ToList();

      Dictionary<int, ProtoFluxNode> spawnedNodes = [];

      ProtoFluxNode RootNode = null;

      List<Type> InvalidTypes =
      [
        typeof(ExternalImpulseDisplay<>),
        typeof(ExternalObjectInput<,>),
        typeof(ExternalValueInput<,>),
        typeof(ExternalObjectDisplay<,>),
        typeof(ExternalValueDisplay<,>)
      ];

      for (int i = 0; i < spawningTypes.Count; i++)
      {
        Type targetType = null;
        Type spawningType = spawningTypes[i];
        spawningType.TryGetGenericTypeDefinition(out Type genericType);
        int index = InvalidTypes.IndexOf(genericType);
        if (index != -1)
        {
          int argLen = spawningType.GenericTypeArguments.Length;
          Type realType = spawningType.GenericTypeArguments[argLen - 1];

          switch (index)
          {
            case 0:
              targetType = ProtoFluxHelper.CallDisplay;
              break;
            case 1:
            case 2:
              targetType = ProtoFluxHelper.GetInputNode(realType);
              break;
            case 3:
            case 4:
              targetType = ProtoFluxHelper.GetDisplayNode(realType);
              break;
          }

        }
        targetType ??= ProtoFluxHelper.GetBindingForNode(spawningType);
        try
        {
          tool.SpawnNode(targetType, node =>
          {
            node.EnsureElementsInDynamicLists();
            spawnedNodes[i] = node;
            node.EnsureVisual();
            if (target.NodeDefinitions[i].IsRoot) RootNode = node;
          });
        }
        catch
        {
          spawnedNodes[i] = null;
        }
      }

      await new Updates(3);

      if (spawnedNodes.Values.Any(node => node == null))
      {
        foreach (ProtoFluxNode node in spawnedNodes.Values)
        {
          if (node != null) node.Slot.Destroy();
        }
        return;
      }

      if (RootNode == null)
      {
        foreach (ProtoFluxNode node in spawnedNodes.Values)
        {
          node.Slot.Destroy();
        }
        return;
      }

      tool.World.BeginUndoBatch($"Create Recipe: {target.RecipeName}");

      foreach (ProtoFluxNode node in spawnedNodes.Values)
      {
        node.Slot.CreateSpawnUndoPoint("Spawn Node");
        node.Slot.SetParent(Into ?? RootNode.Slot.Parent);
      }

      try
      {
        // Position stuff
        Slot rootNodeSlot = RootNode.Slot;
        Slot rootParent = rootNodeSlot.Parent;
        float3 baseUp = rootNodeSlot.Up;
        float3 baseRight = rootNodeSlot.Right;
        float3 baseForward = rootNodeSlot.Forward;
        float3 scaled = rootNodeSlot.GlobalScale;

        void LocalTransformNode(ProtoFluxNode input, float3 offset)
        {
          Slot target = input.Slot;
          target.CopyTransform(rootNodeSlot);
          float3 offsetFactor = (baseRight * offset.X) + (baseUp * offset.Y) + (baseForward * offset.Z);
          target.GlobalPosition += offsetFactor * scaled;
        }

        for (int i = 0; i < target.NodeDefinitions.Count; i++)
        {
          NodeDef thisNodeDef = target.NodeDefinitions[i];
          List<byte3> cons = thisNodeDef.NodeConnections;
          //if (cons.Count == 0) continue;
          ProtoFluxNode thisNode = spawnedNodes[i];
          cons.ForEach((con) =>
          {
            if (con.x == 0xff)
            {
              if (DontConnectOut) return;
              if (target.IsOutputProxy)
              {
                if (proxy is not ProtoFluxOutputProxy outProxy) return;
                ISyncRef NodeInput = thisNode.GetInput(con.z);
                thisNode.TryConnectInput(NodeInput, outProxy.NodeOutput.Target, allowExplicitCast: false, undoable: true);
              }
              else
              {
                if (proxy is not ProtoFluxInputProxy inProxy) return;

                INodeOutput NodeOutput = thisNode.GetOutput(con.z);
                proxy.Node.Target.TryConnectInput(inProxy.NodeInput.Target, NodeOutput, allowExplicitCast: false, undoable: true);
              }
              return;
            }
            ProtoFluxNode from = spawnedNodes[con.x];

            if (con.y < from.NodeOperationCount)
            {
              INodeOperation outputOper = from.GetOperation(con.y);
              ISyncRef input = thisNode.GetImpulse(con.z);

              input.Target = outputOper;
            }
            else
            {
              INodeOutput output = from.GetOutput(con.y - from.NodeOperationCount);
              ISyncRef input = thisNode.GetInput(con.z - thisNode.NodeImpulseCount);

              input.Target = output;
            }
          });

          float3 offset = thisNodeDef.Offset;

          LocalTransformNode(thisNode, offset);
        }
        tool.World.EndUndoBatch();

      }
      catch
      {
        tool.World.EndUndoBatch();
        foreach (ProtoFluxNode node in spawnedNodes.Values)
        {
          node.Slot.Destroy();
        }
        throw;
      }

    });
  }

  public static void RecipeFromSlot(Slot target, MakerDynVars dynVars)
  {
    if (target.ChildrenCount == 0) return;

    string recipeName = dynVars.recipeName;
    bool recipeIsOutput = dynVars.isOutput;
    Slot recipeRootSlot = dynVars.rootNode;
    Type recipeType = dynVars.rootType;
    colorX? recipeColor = dynVars.customColor;
    string? group = dynVars.group;


    if (string.IsNullOrEmpty(recipeName)) return;

    Slot rootNode = recipeRootSlot ?? target[0];
    ProtoFluxNode rootNodeNode = rootNode.GetComponent<ProtoFluxNode>();
    if (rootNodeNode == null) return;

    List<ProtoFluxNode> nodes = rootNode.Parent.GetComponentsInChildren<ProtoFluxNode>();
    nodes.RemoveAll(node => !node.Slot.PersistentSelf); // non persistent nodes are nodes we dont want in here
    nodes.OrderBy(node => node == rootNodeNode).ThenBy(node => node.Slot.ChildIndex);
    FluxRecipe newRecipe = new()
    {
      RecipeName = recipeName,
      IsOutputProxy = recipeIsOutput,
      AllowedProxyTypes = [recipeType],
      NodeDefinitions = [],
      customColor = recipeColor,
      group = group
    };

    Dictionary<INodeOperation, int2> NodeImpulseOut = [];
    Dictionary<INodeOutput, int2> NodeOutputs = [];
    Dictionary<int2, ISyncRef> NodeInputRefs = [];
    for (int i = 0; i < nodes.Count; i++)
    {
      ProtoFluxNode node = nodes[i];
      for (int j = 0; j < node.NodeOutputCount + node.NodeOperationCount; j++)
      {
        int2 key = new(i, j);
        if (j < node.NodeOperationCount) NodeImpulseOut.Add(node.GetOperation(j), key);
        else NodeOutputs.Add(node.GetOutput(j - node.NodeOperationCount), key);

      }
      for (int j = 0; j < node.NodeInputCount + node.NodeImpulseCount; j++)
      {
        int2 key = new(i, j);
        if (j < node.NodeImpulseCount) NodeInputRefs.Add(key, node.GetImpulse(j));
        else NodeInputRefs.Add(key, node.GetInput(j - node.NodeImpulseCount));
      }
    }

    Dictionary<int, List<byte3>> NodeConnectionValues = [];

    foreach (var kv in NodeInputRefs)
    {
      int2 key = kv.Key;
      ISyncRef val = kv.Value;

      int node = key.x;
      int field = key.y;

      if (val.Target is INodeOutput targetOutput)
      {
        bool found = NodeOutputs.TryGetValue(targetOutput, out int2 fromNode);

        byte targetNode = 0xff;
        byte targetNodeIndex = 0;

        if (found)
        {
          targetNode = (byte)fromNode.x;
          targetNodeIndex = (byte)fromNode.y;
        }

        byte3 output = new(targetNode, targetNodeIndex, (byte)field);
        bool foundList = NodeConnectionValues.TryGetValue(node, out List<byte3>? thisNodeList);
        if (!found)
        {
          NodeConnectionValues.Add(node, []);
          thisNodeList = NodeConnectionValues[node];
        }
        thisNodeList ??= [];
        thisNodeList.Add(output);
        NodeConnectionValues[node] = thisNodeList;
      }
      if (val.Target is INodeOperation targetOperation)
      {
        bool found = NodeImpulseOut.TryGetValue(targetOperation, out int2 fromNode);

        byte targetNode = 0xff;
        byte targetNodeIndex = 0;

        if (found)
        {
          targetNode = (byte)fromNode.x;
          targetNodeIndex = (byte)fromNode.y;
        }

        byte3 output = new(targetNode, targetNodeIndex, (byte)field);
        bool foundList = NodeConnectionValues.TryGetValue(node, out List<byte3>? thisNodeList);
        if (!found)
        {
          NodeConnectionValues.Add(node, []);
          thisNodeList = NodeConnectionValues[node];
        }
        thisNodeList ??= [];
        thisNodeList.Add(output);
        NodeConnectionValues[node] = thisNodeList;
      }
    }

    float3 rootPos = rootNode.LocalPosition;

    float3 relativePos(ProtoFluxNode node)
    {
      if (node == rootNodeNode) return float3.Zero;
      float3 globalPos = node.Slot.GlobalPosition;
      return rootNode.GlobalPointToLocal(in globalPos);
    }

    for (int i = 0; i < nodes.Count; i++)
    {
      ProtoFluxNode node = nodes[i];
      NodeConnectionValues.TryGetValue(i, out List<byte3>? connections);
      Type thisNodeType = node.NodeType;
      object? extraNodeData = null;
      if (thisNodeType.IsGenericType && thisNodeType.GetGenericTypeDefinition() == typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.ValueInput<>))
      {
        var syncValue = Convert.ChangeType(node.GetSyncMember(3), typeof(Sync<>).MakeGenericType(thisNodeType.GenericTypeArguments[0]));
        if (syncValue != null)
        {
          extraNodeData = Traverse.Create(syncValue).Field("Value").GetValue();
        }
      }
      NodeDef thisNode = new()
      {
        NodeType = thisNodeType,
        IsRoot = node == rootNodeNode,
        IsTypeNull = thisNodeType == null,
        NodeConnections = connections ?? [],
        Offset = relativePos(node),
        ObjectData = extraNodeData
      };

      newRecipe.NodeDefinitions.Add(thisNode);
    }

    OnAddItem(newRecipe);
  }
}

public struct FluxRecipe(string name, bool isOutput, List<Type?> types, List<NodeDef> nodes, colorX? customColor = null, string? group = null)
{
  public string RecipeName = name;
  public bool IsOutputProxy = isOutput;
  public List<Type?> AllowedProxyTypes = types;
  public List<NodeDef> NodeDefinitions = nodes;
  public colorX? customColor = customColor;
  public string? group = group;
}

public struct NodeDef(bool root, Type? node, float3 offset, List<byte3> connections, object? extraData = null)
{
  public bool IsRoot = root;

  [JsonIgnore]
  public bool IsTypeNull = node == null;
  public Type NodeType = node ?? typeof(object);

  public float3 Offset = offset;

  public List<byte3> NodeConnections = connections;

  public object? ObjectData = extraData;
}

public struct MakerDynVars(string recipeName, bool isOutput, Slot rootNode, Type rootType, colorX? customColor = null, string? group = null)
{
  public string recipeName = recipeName;
  public bool isOutput = isOutput;
  public Slot rootNode = rootNode;
  public Type rootType = rootType;
  public colorX? customColor = customColor;
  public string? group = group;
}