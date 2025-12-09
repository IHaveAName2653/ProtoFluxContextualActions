using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.ProtoFlux.Runtimes.Execution;
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


/// <summary>
/// Interface for Recipe Config Management. Get/Names/Set/Add. Anything to do with the pure data.
/// </summary>
public class RecipeConfigDynOverride : DynOverride
{
  public override bool InvokeOverride(string FunctionName, Slot target, DynamicVariableSpace? variableSpace, bool excludeDisabled, FrooxEngineContext context)
  {
    if (variableSpace == null) return true;
    switch (FunctionName)
    {
      case "Get":
        {
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "RecipeName", out string recipeName)) return true;
          if (string.IsNullOrEmpty(recipeName)) return true;
          DynSpaceHelper.ReturnFromFunc(variableSpace, 0, "RecipeData", FluxRecipeConfig.GetStringFor(recipeName));
          return false;
        }
      case "GetAll":
        {
          DynSpaceHelper.ReturnFromFunc(variableSpace, 0, "AllRecipeData", FluxRecipeConfig.StringFromData());
          return false;
        }
      case "GetAllNames":
        {
          DynSpaceHelper.ReturnFromFunc(variableSpace, 0, "AllNames", string.Join(",", FluxRecipeConfig.FluxRecipes.Select(recipe => recipe.RecipeName)));
          return false;
        }

      case "AddRecipeString":
        {
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "RecipeString", out string recipeData)) return true;
          if (!string.IsNullOrEmpty(recipeData))
          {
            FluxRecipeConfig.LoadSingleString(recipeData);
            return false;
          }
          return true;
        }
      case "AddMultiple":
        {
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "RecipeStringArray", out string recipeArray)) return true;
          if (!string.IsNullOrEmpty(recipeArray))
          {
            FluxRecipeConfig.LoadMultiString(recipeArray);
            return false;
          }
          return true;
        }


      case "RemoveRecipe":
        {
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "TargetRecipe", out string targetRecipe)) return true;
          if (string.IsNullOrEmpty(targetRecipe)) return true;
          FluxRecipeConfig.OnRemoveItem(targetRecipe);
          return false;
        }

      case "SetAll":
        {
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "AllRecipes", out string recipeBlock)) return true;
          if (!string.IsNullOrEmpty(recipeBlock))
          {
            FluxRecipeConfig.LoadFromString(recipeBlock);
            return false;
          }
          return true;
        }

    }
    return true;
  }
}
/// <summary>
/// Interface for Recipe Data Management. EnsureVar_[type], Reload, 
/// </summary>
public class RecipeDataDynOverride : DynOverride
{
  public override bool InvokeOverride(string FunctionName, Slot target, DynamicVariableSpace? variableSpace, bool excludeDisabled, FrooxEngineContext context)
  {
    if (variableSpace == null) return true;
    switch (FunctionName)
    {
      case "Reload":
        FluxRecipeConfig.ReadFromConfig();
        return false;
    }
    return true;
  }
}

/// <summary>
/// Interface for the Recipe Maker. Specifically handles making recipes, like AddRecipe/DetermineRoot/BuildToSlot.
/// </summary>
public class RecipeMakerDynOverride : DynOverride
{
  public override bool InvokeOverride(string FunctionName, Slot target, DynamicVariableSpace? variableSpace, bool excludeDisabled, FrooxEngineContext context)
  {
    if (variableSpace == null) return true;

    switch (FunctionName)
    {
      case "AddRecipe":
        {
          bool varsExist = true;
          varsExist &= DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "RecipeSlot", out Slot recipeSlot) || recipeSlot == null;
          varsExist &= DynSpaceHelper.TryGetArgOrName(variableSpace, 1, "RecipeName", out string recipeName) || recipeName == null;
          varsExist &= DynSpaceHelper.TryGetArgOrName(variableSpace, 2, "RecipeRootNode", out Slot recipeRootNode) || recipeRootNode == null;
          varsExist &= DynSpaceHelper.TryGetArgOrName(variableSpace, 3, "RecipeType", out Type recipeType) || recipeType == null;
          varsExist &= DynSpaceHelper.TryGetArgOrName(variableSpace, 4, "RecipeIsOutput", out bool recipeIsOutput);
          DynSpaceHelper.TryGetArgOrName(variableSpace, 5, "RecipeColor", out colorX? recipeColor);
          if (!varsExist) return true;
          if (recipeSlot == null) return true;
          if (string.IsNullOrEmpty(recipeName)) return true;
          if (recipeRootNode == null) return true;
          if (recipeType == null) return true;


          var recipeVarProvider = new MakerDynVars(recipeName, recipeIsOutput, recipeRootNode, recipeType, recipeColor);
          FluxRecipeConfig.RecipeFromSlot(recipeSlot, recipeVarProvider);
          return false;
        }
      case "DetermineRoot":
        {
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "TargetNode", out Slot targetNode)) return true;
          if (targetNode == null) return true;
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 1, "VariableRoot", out Slot varRoot)) return true;
          ProtoFluxNode node = targetNode.GetComponent<ProtoFluxNode>();
          if (node == null) return true;
          Type nodeType = node.NodeType;
          if (!nodeType.IsGenericType) return true;
          Type genericTypeDef = nodeType.GetGenericTypeDefinition();
          if (genericTypeDef == null) return true;
          if (!(genericTypeDef == typeof(ValueRelay<>) || genericTypeDef == typeof(ObjectRelay<>))) return true;
          Type genericType = nodeType.GenericTypeArguments[^1];
          bool isOutput = false;
          Slot objectSlot = ((Component)node.GetInput(0).Target).Slot;
          if (objectSlot != null)
          {
            ProtoFluxNode connectedNode = objectSlot.GetComponent<ProtoFluxNode>();
            Type inputNodeType = connectedNode.NodeType;
            if (inputNodeType.IsGenericType)
            {
              Type? inputType = inputNodeType.GetGenericTypeDefinition();
              if (inputType == typeof(ExternalValueInput<,>) || inputType == typeof(ExternalObjectInput<,>)) isOutput = true;
            }
          }
          else isOutput = true;
          DynSpaceHelper.TryWrite(variableSpace, "RecipeIsOutput", isOutput, true);
          DynSpaceHelper.TryWrite(variableSpace, "RecipeRootNode", targetNode, true);
          DynSpaceHelper.TryWrite(variableSpace, "RecipeType", genericType, true);
          return false;
        }
      case "BuildToSlot":
        {
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "RecipeName", out string targetRecipe)) return true;
          if (string.IsNullOrEmpty(targetRecipe)) return true;
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 1, "TargetParent", out Slot targetParent)) return true;
          if (targetParent == null) return true;
          if (!DynSpaceHelper.TryGetArgOrName(variableSpace, 2, "FluxTool", out ProtoFluxTool fluxTool)) return true;
          if (fluxTool == null) return true;
          FluxRecipe? ActualRecipe = FluxRecipeConfig.FluxRecipes.Find(recipe => recipe.RecipeName == targetRecipe);
          if (targetRecipe == null) return true;
          FluxRecipeConfig.ConstructFluxRecipe(
              fluxTool,
              null,
              ActualRecipe.Value, true, targetParent
          );
          return false;
        }
    }

    return true;
  }
}









/// <summary>
/// Legacy Interface for Recipes. Handles everything to do with recipes, including Finding, Getting, Constructing and Adding.
/// NOTE: Do not use this interface. This is just so anything that uses the old interface doesnt break
/// </summary>
public class LegacyRecipeStringInterface : DynOverride
{
  public override bool InvokeOverride(string FunctionName, Slot hierarchy, DynamicVariableSpace? variableSpace, bool excludeDisabled, FrooxEngineContext context)
  {
    string[] parts = FunctionName.Split("/");
    string target = parts[0];
    string variable = parts.Length >= 2 ? parts[1] : "";

    if (target == "EnsureVars_Data")
    {
      var recipeSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeData", true);
      DynSpaceHelper.EnsureVariableExists<string>(recipeSpace, "ThisRecipe");
      DynSpaceHelper.EnsureVariableExists<string>(recipeSpace, "AllRecipes");
      DynSpaceHelper.EnsureVariableExists<string>(recipeSpace, "AllNames");
      DynSpaceHelper.EnsureVariableExists<string>(recipeSpace, "AllRecipeString");
      DynSpaceHelper.EnsureVariableExists<string>(recipeSpace, "Recipe");
    }
    if (target == "EnsureVars_Maker")
    {
      var makerSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeMaker", true);
      DynSpaceHelper.EnsureVariableExists<bool>(makerSpace, "RecipeIsOutput");
      DynSpaceHelper.EnsureVariableExists<Slot>(makerSpace, "RecipeRootNode");
      DynSpaceHelper.EnsureVariableExists<Type>(makerSpace, "RecipeType");
      DynSpaceHelper.EnsureVariableExists<string>(makerSpace, "RecipeName");
    }
    if (target == "EnsureVars_Construct")
    {
      var constructSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxConstructData", true);
      DynSpaceHelper.EnsureVariableExists<ProtoFluxTool>(constructSpace, "Tool");
    }

    if (target == "AddRecipe")
    {
      if (hierarchy == null) return true;
      var makerSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeMaker", true);
      bool varsExist = true;
      varsExist &= DynSpaceHelper.TryRead(makerSpace, "RecipeName", out string recipeName) && !string.IsNullOrEmpty(recipeName);
      varsExist &= DynSpaceHelper.TryRead(makerSpace, "RecipeRootNode", out Slot recipeRootNode) && recipeRootNode != null;
      varsExist &= DynSpaceHelper.TryRead(makerSpace, "RecipeType", out Type recipeType) && recipeType != null;
      varsExist &= DynSpaceHelper.TryRead(makerSpace, "RecipeIsOutput", out bool recipeIsOutput);
      DynSpaceHelper.TryRead(makerSpace, "RecipeColor", out colorX? recipeColor);

      if (!varsExist) return true;
      if (string.IsNullOrEmpty(recipeName)) return true;
      if (recipeRootNode == null) return true;
      if (recipeType == null) return true;

      var recipeVarProvider = new MakerDynVars(recipeName, recipeIsOutput, recipeRootNode, recipeType, recipeColor);
      FluxRecipeConfig.RecipeFromSlot(hierarchy, recipeVarProvider);
      return false;
    }
    if (target == "DetermineRoot")
    {
      if (hierarchy == null) return true;
      var makerSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeMaker");
      if (makerSpace == null) return true;
      ProtoFluxNode node = hierarchy.GetComponent<ProtoFluxNode>();
      if (node == null) return true;
      Type nodeType = node.NodeType;
      if (!nodeType.IsGenericType) return true;
      Type genericTypeDef = nodeType.GetGenericTypeDefinition();
      if (genericTypeDef == null) return true;
      if (!(genericTypeDef == typeof(ValueRelay<>) || genericTypeDef == typeof(ObjectRelay<>))) return true;
      Type genericType = nodeType.GenericTypeArguments[^1];
      bool isOutput = false;
      Slot objectSlot = ((Component)node.GetInput(0).Target).Slot;
      if (objectSlot != null)
      {
        ProtoFluxNode connectedNode = objectSlot.GetComponent<ProtoFluxNode>();
        Type inputNodeType = connectedNode.NodeType;
        if (inputNodeType.IsGenericType)
        {
          Type? inputType = inputNodeType.GetGenericTypeDefinition();
          if (inputType == typeof(ExternalValueInput<,>) || inputType == typeof(ExternalObjectInput<,>)) isOutput = true;
        }
      }
      else isOutput = true;
      DynSpaceHelper.TryWrite(makerSpace, "RecipeIsOutput", isOutput, true);
      DynSpaceHelper.TryWrite(makerSpace, "RecipeRootNode", hierarchy, true);
      DynSpaceHelper.TryWrite(makerSpace, "RecipeType", genericType, true);
    }
    if (target == "BuildToSlot")
    {
      if (string.IsNullOrEmpty(variable)) return true;
      if (hierarchy == null) return true;
      var constructSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxConstructData", true);
      if (DynSpaceHelper.TryRead(constructSpace, "Tool", out ProtoFluxTool fluxTool, true))
      {
        if (fluxTool == null) return true;
        FluxRecipe? targetRecipe = FluxRecipeConfig.FluxRecipes.Find(recipe => recipe.RecipeName == variable);
        if (targetRecipe == null) return true;
        FluxRecipeConfig.ConstructFluxRecipe(
            fluxTool,
            null,
            targetRecipe.Value, true, hierarchy
        );
        return false;
      }
    }
    if (target == "RemoveRecipe")
    {
      if (string.IsNullOrEmpty(variable)) return true;
      FluxRecipeConfig.OnRemoveItem(variable);
      return false;
    }
    if (target == "Reload")
    {
      FluxRecipeConfig.ReadFromConfig();
      return false;
    }
    if (target == "Get")
    {
      if (string.IsNullOrEmpty(variable)) return true;
      var recipeSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeData", true);
      DynSpaceHelper.TryWrite(recipeSpace, "ThisRecipe", FluxRecipeConfig.GetStringFor(variable), true);
      return false;
    }
    if (target == "GetAll")
    {
      var recipeSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeData", true);
      DynSpaceHelper.TryWrite(recipeSpace, "AllRecipes", FluxRecipeConfig.StringFromData(), true);
      return false;
    }
    if (target == "GetAllNames")
    {
      var recipeSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeData", true);
      DynSpaceHelper.TryWrite(recipeSpace, "AllNames", string.Join(",", FluxRecipeConfig.FluxRecipes.Select(recipe => recipe.RecipeName)), true);
      return false;
    }
    if (target == "SetAll")
    {
      if (!string.IsNullOrEmpty(variable))
      {
        FluxRecipeConfig.LoadFromString(variable);
        return false;
      }
      else
      {
        var recipeSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeData", true);
        if (DynSpaceHelper.TryRead(recipeSpace, "AllRecipeString", out string content, true))
        {
          if (string.IsNullOrEmpty(content)) return true;
          FluxRecipeConfig.LoadFromString(content);
          return false;
        }
      }
    }
    if (target == "AddRecipeString")
    {
      if (!string.IsNullOrEmpty(variable))
      {
        FluxRecipeConfig.LoadSingleString(variable);
        return false;
      }
      else
      {
        var recipeSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeData", true);
        if (DynSpaceHelper.TryRead(recipeSpace, "Recipe", out string content, true))
        {
          if (string.IsNullOrEmpty(content)) return true;
          FluxRecipeConfig.LoadSingleString(content);
          return false;
        }
      }
    }
    if (target == "AddMultiple")
    {
      if (!string.IsNullOrEmpty(variable))
      {
        FluxRecipeConfig.LoadMultiString(variable);
        return false;
      }
      else
      {
        var recipeSpace = DynSpaceHelper.TryGetSpace(hierarchy, "FluxRecipeData", true);
        if (DynSpaceHelper.TryRead(recipeSpace, "Recipes", out string content, true))
        {
          if (string.IsNullOrEmpty(content)) return true;
          FluxRecipeConfig.LoadMultiString(content);
          return false;
        }
      }
    }
    return true;
  }
}