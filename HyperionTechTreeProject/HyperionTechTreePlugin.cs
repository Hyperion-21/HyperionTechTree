﻿using BepInEx;
using HarmonyLib;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using BepInEx.Logging;
using System.Runtime.InteropServices.ComTypes;
using Newtonsoft.Json;

namespace HyperionTechTree;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class HyperionTechTreePlugin : BaseSpaceWarpPlugin
{
    // REMEMBER! 
    // Use _camelCase for private variables
    // Use camelCase for local variables (defined in method)
    // Use PascalCase for just about everything else

    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;
    
    private bool _isWindowOpen;
    private Rect _windowRect;

    private const string ToolbarFlightButtonID = "BTN-HyperionTechTreeFlight";
    private const string ToolbarOABButtonID = "BTN-HyperionTechTreeOAB";

    private static JsonTextReader _reader;
    private static string _jsonFilePath;

    private string DefaultTechTreeFilePath => $"{PluginFolderPath}/Tech Tree/TechTree.json";

    private static ManualLogSource _logger;

    // private static JsonTextReader _testreader;

    public static HyperionTechTreePlugin Instance { get; set; }

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();
        _logger = Logger;

        Instance = this;

        // Register Flight AppBar button
        Appbar.RegisterAppButton(
            "Hyperion Tech Tree",
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );

        // Register OAB AppBar Button
        Appbar.RegisterOABAppButton(
            "Hyperion Tech Tree",
            ToolbarOABButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );

        // Register all Harmony patches in the project
        Harmony.CreateAndPatchAll(typeof(HyperionTechTreePlugin).Assembly);

        //// Try to get the currently active vessel, set its throttle to 100% and toggle on the landing gear
        //// (might use part of this code later for science point awarding)
        //try
        //{
        //    var currentVessel = Vehicle.ActiveVesselVehicle;
        //    if (currentVessel != null)
        //    {
        //        currentVessel.SetMainThrottle(1.0f);
        //        currentVessel.SetGearState(true);
        //    }
        //}
        //
        //catch (Exception e) {}

        // Fetch a configuration value or create a default one if it does not exist
        //var defaultValue = "my_value";
        //var configValue = Config.Bind<string>("Settings section", "Option 1", defaultValue, "Option description");

        //// Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        //Logger.LogInfo($"Option 1: {configValue.Value}");

        // Change this later to support multiple files
        _logger.LogInfo($"Creating json reader from {DefaultTechTreeFilePath}");
        if (File.Exists(DefaultTechTreeFilePath)) CreateJsonReader(DefaultTechTreeFilePath);
        else _logger.LogInfo($"Could not find file at {DefaultTechTreeFilePath}!");

    }

    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "Hyperion Tech Tree",
                GUILayout.Height(350),
                GUILayout.Width(350)
            );
        }
    }

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private static void FillWindow(int windowID)
    {
        //string nodeID = (string)GetValueFromKey("nodeID");
        //_logger.LogInfo(nodeID);
        
        //string[] dependencies = (string[])_reader.GetValueFromKey("dependencies");
        //string[] parts = (string[])_reader.GetValueFromKey("parts");

        //GUILayout.Label($"First Node ID: {nodeID}");
        //GUILayout.Label(dependencies.Length > 0 ? $"First Node First Dependency: {dependencies[0]}" : "There are no dependencies for this node!");
        //GUILayout.Label(parts.Length > 0 ? $"First Node First Part: {parts[0]}" : "There are no parts for this node!");

        GUI.DragWindow(new Rect(0, 0, 10000, 500));
    }






    /// <summary>
    /// A json reader class made by Hyperion_21. Construct using a file path to the target .json file.
    /// </summary>
    /// <param name="jsonFilePath"></param>
    private void CreateJsonReader(string jsonFilePath)
    {
        _jsonFilePath = jsonFilePath;
        var jsonString = File.ReadAllText(_jsonFilePath);
        dynamic jsonObject = JsonConvert.DeserializeObject(jsonString);
        //_logger.LogInfo($"Print of first nodeID: {jsonObject.nodes[0].nodeID}");

        foreach (var node in jsonObject.nodes)
        {
            _logger.LogInfo($"Found nodeID {node.nodeID}!");
        }
        

        //_reader = new(new StringReader(jsonString));
        //_logger.LogInfo($"jsonString: {jsonString}");


    }

    /*
    /// <summary>
    /// Inputs a key to look for in the .json, and returns that key's value. Return type depends on the key, so explicit casts may be needed!
    /// </summary>
    /// <param name="key"></param>
    /// <returns name="value"></returns>
    private static object GetValueFromKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogError("Could not run GetValueFromKey! Returning null.");
            return null;
        }
        _logger.LogInfo($"GetValueFromKey run for key {key}.");
        while (_reader.Read())
        {
            if (_reader.Value == null) continue;
            if (_reader.Value.ToString() == key)
            {
                break;
            }
        }
        _reader.Read();
        var value = _reader.Value;
        _logger.LogInfo($"Found value {value}!");
        ResetJsonReader();
        return value;
    }

    private static void ResetJsonReader()
    {
        _logger.LogInfo("Attempting to reset json reader!");
        _reader.Close();
        //_reader = null;
        _reader = new(new StringReader(_jsonFilePath));
    }
    */
}