using BepInEx;
using HarmonyLib;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using BepInEx.Logging;
using Newtonsoft.Json;
using KSP.OAB;
using I2.Loc;
using KSP.Game;
using SpaceWarp.API.Game;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.UI;
using KSP.Iteration.UI.Binding;
using static KSP.Api.UIDataPropertyStrings.View.Vessel.Stages;
using static HyperionTechTree.KerbalProbeManager;
using System.Linq.Expressions;

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

    private static bool _isWindowOpen;
    private static Rect _windowRect = new(0, 0, _windowWidth, _windowHeight);
    private static float _windowHeight = 1;
    private static float _windowWidth = 1;
    private const float ButtonSize = 36;
    private const float LineWidth = 2;
    private static float _techTreex1 = 10000;
    private static float _techTreex2 = -10000;
    private static float _techTreey1 = 10000;
    private static float _techTreey2 = -10000;
    private static Vector2 _scrollbarPos1 = Vector2.zero;
    private static Vector2 _scrollbarPos2 = Vector2.zero;
    private static Vector2 _scrollbarPos3 = Vector2.zero;
    private static List<string> _sciradLog = new();
    private static Dictionary<string, bool> _collapsableList = new();
    private enum WindowTabs
    {
        TechTree,
        Goals,
        ModInfo
    }
    private static WindowTabs _windowTab = WindowTabs.TechTree;

    internal enum CraftSituation
    {
        Landed,
        LowAtmosphere,
        HighAtmosphere,
        LowSpace,
        HighSpace,
        Orbit
    }
    internal static readonly Dictionary<CraftSituation, string> CraftSituationSpaced = new()
    {
        { CraftSituation.Landed, "Landed" },
        { CraftSituation.LowAtmosphere, "Low Atmosphere" },
        { CraftSituation.HighAtmosphere, "High Atmosphere" },
        { CraftSituation.LowSpace, "Low Space" },
        { CraftSituation.HighSpace, "High Space" },
        { CraftSituation.Orbit, "Orbit" }
    };
    internal static readonly Dictionary<CraftSituation, string> CraftSituationSpacedShort = new()
    {
        { CraftSituation.Landed, "Landed" },
        { CraftSituation.LowAtmosphere, "Low Atmo" },
        { CraftSituation.HighAtmosphere, "High Atmo" },
        { CraftSituation.LowSpace, "Low Space" },
        { CraftSituation.HighSpace, "High Space" },
        { CraftSituation.Orbit, "Orbit" }
    };
    private const float ScienceSecondsOfDelay = 10;
    private static float _remainingTime = float.MaxValue;
    private static float _awardAmount = 0;
    internal static bool _orbitScienceFlag = false;

    internal static CraftSituation _craftSituation = CraftSituation.Landed;
    internal static CraftSituation _craftSituationOld = CraftSituation.Landed;
    internal static Dictionary<string, Dictionary<CraftSituation, int>> _situationOccurances = new();

    protected static bool _isCraftOrbiting = false;
    // _craftSituation is never set to Orbit, use _isCraftOrbiting instead. That enum value is there for tracking situation occurances.


    //private static Dictionary<string, List<string>> _scienceLicenses = new();

    private static VesselComponent _simVessel;
    private static VesselVehicle _vesselVehicle;

    private static Texture2D _tex = new(1, 1);

    private const string ToolbarFlightButtonID = "BTN-HyperionTechTreeFlight";
    private const string ToolbarOABButtonID = "BTN-HyperionTechTreeOAB";

    private static string _jsonFilePath;
    private static TechTree _jsonTechTree;
    internal static Goals _jsonGoals;

    private static Dictionary<string, bool> _techsObtained = new();
    private static List<TechTreeNode> _techTreeNodes = new();
    internal static List<GoalsBody> GoalsList = new();


    private static float _techPointBalance = 100000;

    private static TechTreeNode _focusedNode = null;

    private static List<AssemblyPartsButton> _assemblyPartsButtons = new();

    // Whenever typing a file path, use {_s} instead of / or \ or \\ unless the function specifically needs /
    // If in doubt, use {_s}, and if it causes a crash replace with / (NOT \ OR \\)
    // {_s} looks better when printed to log, "abc/def\ghi" vs "abc\def\ghi"
    // Some things that look like filepaths actually aren't (i.e. AssetManager.GetAsset calls),
    // and using \, \\, or {_s} there can cause crashes!
    // (because of course the difference between / and \ becomes relevant in this code)
    private static readonly char _s = System.IO.Path.DirectorySeparatorChar;
    private static string PluginFolderPathStatic;
    private static string DefaultTechTreeFilePath => $"{PluginFolderPathStatic}{_s}Tech Tree{_s}DefaultTechTree.json";
    private static string DefaultGoalsFilePath => $"{PluginFolderPathStatic}{_s}Goals{_s}DefaultGoals.json";
    private static string _UTdisplay;
    private static string _path;
    private static string _swPath;

    internal static ManualLogSource HLogger;
    private static ManualLogSource _logger;
    private static bool _disableMod = false;
    internal static string Path = PluginFolderPathStatic;

    public static HyperionTechTreePlugin Instance { get; set; }

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();
        HLogger = Logger;
        _logger = Logger;
        PluginFolderPathStatic = PluginFolderPath;
        Path = PluginFolderPathStatic;

        // Completely disables the mod if Q and P are held down during startup
        if (Input.GetKey(KeyCode.Q) && Input.GetKey(KeyCode.P))
        {
            _disableMod = true;
            _logger.LogWarning("\n\n" +
                "/////////////////////////////////////////////////////////////////////////\n" +
                "Hyperion Tech Tree has been disabled!\n" +
                "This should have been achieved by holding down Q and P during startup!\n" +
                "If you didn't do this, something went very wrong! Send a bug report!\n" +
                "HTT will be disabled for this game session! Restart the game to reenable!\n" +
                "/////////////////////////////////////////////////////////////////////////\n");
            return;
        }

        _path = PluginFolderPath;
        _swPath = SpaceWarpMetadata.ModID;

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
        Harmony.CreateAndPatchAll(typeof(HyperionTechTreePlugin));
        Harmony.CreateAndPatchAll(typeof(HyperionTechTreePlugin).Assembly);

        
        GenerateTechs();
        GenerateGoals();
        KerbalProbeManagerInitialize();
        GenerateSituationOccurances();
        GeneratePPD();
        GenerateLicenses();

        foreach (var goal in GoalsList)
        {
            _logger.LogInfo("Found celstial body " + goal.BodyName);
            //_situationOccurances.Add(goal.BodyName, new()
            //{
            //    { CraftSituation.Landed, 0 },
            //    { CraftSituation.LowAtmosphere, 0 },
            //    { CraftSituation.HighAtmosphere, 0 },
            //    { CraftSituation.LowSpace, 0 },
            //    { CraftSituation.HighSpace, 0 },
            //    { CraftSituation.Orbit, 0 },
            //});
            
        }

        

    }

    private void Update()
    {
        try
        {
            _remainingTime -= Time.deltaTime * Game.UniverseView.UniverseTime.PhysicsToUniverseMultiplier;
        } 
        catch 
        {
            _remainingTime -= Time.deltaTime;
        }
        

        if (Game?.GlobalGameState?.GetState() != GameState.FlightView) return;

        UpdateKPM();
        GenerateLicenses();


        _simVessel = SimVessel;
        _vesselVehicle = KerbalProbeManager.VesselVehicle;
        if (_simVessel == null) return;

        

        _awardAmount = 0;
        foreach (var body in GoalsList)
        {
            if (body.BodyName != _simVessel.mainBody.bodyName) continue;




            

            if (_simVessel.Situation == VesselSituations.Orbiting && !CheckSituationClaimed(CraftSituation.Orbit) && _orbitScienceFlag)
            {
                _awardAmount = (float)(body.OrbitAward / Math.Pow(2.0, (double)_situationOccurances[body.BodyName][CraftSituation.Orbit]));

            }
            else if ((int)_simVessel.Situation <= 2 && body.HasSurface)
            {
                _craftSituation = CraftSituation.Landed;
                _awardAmount = (float)(body.LandedAward / Math.Pow(2.0, (double)_situationOccurances[body.BodyName][CraftSituation.Landed]));
            }
            else if (_simVessel.IsInAtmosphere && _simVessel.AltitudeFromSeaLevel < body.AtmosphereThreshold && (int)_simVessel.Situation > 2 && body.HasAtmosphere)
            {
                _craftSituation = CraftSituation.LowAtmosphere;
                _awardAmount = (float)(body.LowAtmosphereAward / Math.Pow(2.0, (double)_situationOccurances[body.BodyName][CraftSituation.LowAtmosphere]));
            }
            else if (_simVessel.IsInAtmosphere && _simVessel.AltitudeFromSeaLevel >= body.AtmosphereThreshold && body.HasAtmosphere)
            {
                _craftSituation = CraftSituation.HighAtmosphere;
                _awardAmount = (float)(body.HighAtmosphereAward / Math.Pow(2.0, (double)_situationOccurances[body.BodyName][CraftSituation.HighAtmosphere]));
            }
            else if (!_simVessel.IsInAtmosphere && _simVessel.AltitudeFromSeaLevel < body.SpaceThreshold)
            {
                _craftSituation = CraftSituation.LowSpace;
                _awardAmount = (float)(body.LowSpaceAward / Math.Pow(2.0, (double)_situationOccurances[body.BodyName][CraftSituation.LowSpace]));
            }
            else if (!_simVessel.IsInAtmosphere && _simVessel.AltitudeFromSeaLevel >= body.SpaceThreshold)
            {
                _craftSituation = CraftSituation.HighSpace;
                _awardAmount = (float)(body.HighSpaceAward / Math.Pow(2.0, (double)_situationOccurances[body.BodyName][CraftSituation.HighSpace]));
            }


            

            
            

            if (_remainingTime < 0)
            {
                _techPointBalance += _awardAmount;
                _logger.LogInfo($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Science complete! Gained {_awardAmount} tech points!");
                _sciradLog.Add($"<color=#00ffff>[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Science complete! Gained {_awardAmount} tech points!</color>");
                _situationOccurances[_simVessel.mainBody.bodyName][_orbitScienceFlag ? CraftSituation.Orbit : _craftSituation]++; 
                _remainingTime = float.MaxValue;
                AddSituationToLicense();
                _scrollbarPos1 = new Vector2(0, float.MaxValue);
                _orbitScienceFlag = false;
            }

            if (_isCraftOrbiting != (_simVessel.Situation == VesselSituations.Orbiting) && !_isCraftOrbiting)
            {
                _remainingTime = ScienceSecondsOfDelay;
                _awardAmount = (float)(body.OrbitAward / Math.Pow(2.0, (double)_situationOccurances[body.BodyName][CraftSituation.Orbit]));
                _logger.LogInfo($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Craft entered orbit. Maintain orbit for {ScienceSecondsOfDelay}s to gain {_awardAmount} tech points!");
                _sciradLog.Add($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Craft entered orbit. Maintain orbit for {ScienceSecondsOfDelay}s to gain {_awardAmount} tech points!");
                _scrollbarPos1 = new Vector2(0, float.MaxValue);
                _orbitScienceFlag = true;
            }
            if (_craftSituation != _craftSituationOld)
            {
                if (CheckSituationClaimed(_craftSituation))
                {
                    _logger.LogInfo($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Already claimed situation {_craftSituationOld}. Going to {_craftSituation}.");
                    _sciradLog.Add($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Already claimed situation {_craftSituationOld}. Going to {_craftSituation}.");
                    _scrollbarPos1 = new Vector2(0, float.MaxValue);
                }
                else
                {
                    if (_remainingTime < ScienceSecondsOfDelay)
                    {
                        _logger.LogInfo($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Previous science interrupted! Going from {_craftSituationOld} to {_craftSituation}. Maintain current state for {ScienceSecondsOfDelay}s to gain {_awardAmount} tech points!");
                        _sciradLog.Add($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] <color=#ff0000>Previous science interrupted!</color> Going from {_craftSituationOld} to {_craftSituation}. Maintain current state for {ScienceSecondsOfDelay}s to gain {_awardAmount} tech points!");
                        _scrollbarPos1 = new Vector2(0, float.MaxValue);
                    }
                    else
                    {
                        _logger.LogInfo($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Craft changing states. Going from {_craftSituationOld} to {_craftSituation}. Maintain the current state for {ScienceSecondsOfDelay}s to gain {_awardAmount} tech points!");
                        _sciradLog.Add($"[{GetHumanReadableUT(GameManager.Instance.Game.UniverseModel.UniversalTime)}] Craft changing states. Going from {_craftSituationOld} to {_craftSituation}. Maintain the current state for {ScienceSecondsOfDelay}s to gain {_awardAmount} tech points!");
                        _scrollbarPos1 = new Vector2(0, float.MaxValue);
                    }
                    _remainingTime = ScienceSecondsOfDelay;
                    
                }
                _craftSituationOld = _craftSituation;
            }
            _isCraftOrbiting = _simVessel.Situation == VesselSituations.Orbiting;

        }
    }

    

    /// <summary>
    /// Generates a save file from various things, and saves it in {Plugin Folder Path}/Saves/SinglePlayer/{Active Campaign Folder Path}/{Save File Name}.json
    /// </summary>
    /// <param name="filepath"></param>
    /// <param name="savedGameType"></param>
    /// <param name="saveOverwriteFileIfExists"></param>
    /// <param name="onLoadOrSaveCampaignFinishedCallback"></param>
    /// <param name="__instance"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SaveGameToFile))]
    [HarmonyPrefix]
    private static bool SaveGameToFile(string filepath, SavedGameType savedGameType, bool saveOverwriteFileIfExists, OnLoadOrSaveCampaignFinishedCallback onLoadOrSaveCampaignFinishedCallback, SaveLoadManager __instance)
    {
        
        string modSavePath = filepath.Substring(filepath.LastIndexOf("Saves"));
        Save save = new();
        save.ModVersion = MyPluginInfo.PLUGIN_VERSION;
        save.TechPointBalance = _techPointBalance;
        save.UnlockedTechs = new();
        foreach (var node in _techsObtained) if (node.Value) save.UnlockedTechs.Add(node.Key);

        save.SituationOccurances = new();

        foreach (var body in GoalsList)
        {
            
            save.SituationOccurances.Add(new()
            {
                BodyName = body.BodyName,
                Landed = _situationOccurances[body.BodyName][CraftSituation.Landed],
                LowAtmosphere = _situationOccurances[body.BodyName][CraftSituation.LowAtmosphere],
                HighAtmosphere = _situationOccurances[body.BodyName][CraftSituation.HighAtmosphere],
                LowSpace = _situationOccurances[body.BodyName][CraftSituation.LowSpace],
                HighSpace = _situationOccurances[body.BodyName][CraftSituation.HighSpace],
                Orbit = _situationOccurances[body.BodyName][CraftSituation.Orbit]
            });

        }

        save.Licenses = new();

        foreach (var kerbal in _kerbalLicenses)
        {
            List<LicenseBody> licenseBodies = new();
            foreach (var body in kerbal.Value)
            {
                licenseBodies.Add(new()
                {
                    BodyName = body.Key,
                    Landed = body.Value.Contains(CraftSituation.Landed),
                    LowAtmosphere = body.Value.Contains(CraftSituation.LowAtmosphere),
                    HighAtmosphere = body.Value.Contains(CraftSituation.HighAtmosphere),
                    LowSpace = body.Value.Contains(CraftSituation.LowSpace),
                    HighSpace = body.Value.Contains(CraftSituation.HighSpace),
                    Orbit = body.Value.Contains(CraftSituation.Orbit)
                });
                _logger.LogInfo("body.key: " + body.Key);
                _logger.LogInfo("body.Value.Contains(CraftSituation.Landed): " + body.Value.Contains(CraftSituation.Landed));
            }
            _logger.LogInfo("kerbal.Key: " + kerbal.Key);
            _logger.LogInfo("licenseBodies: " + licenseBodies);
            save.Licenses.Add(new()
            {
                ID = kerbal.Key,
                Bodies = licenseBodies
            });
        }


        var campaignName = Game.SaveLoadManager.ActiveCampaignFolderPath.Substring(Game.SaveLoadManager.ActiveCampaignFolderPath.LastIndexOf(_s) + 1);
        var fileName = filepath.Substring(filepath.LastIndexOf(_s) + 1);
        //if (!Directory.Exists($"{PluginFolderPathStatic}{_s}Saves{_s}SinglePlayer{_s}{Game.SaveLoadManager.ActiveCampaignFolderPath}")) 
        Directory.CreateDirectory($"{PluginFolderPathStatic}{_s}Saves{_s}SinglePlayer{_s}{campaignName}");
        var serializedJson = JsonConvert.SerializeObject(save);
        File.WriteAllText($"{PluginFolderPathStatic}{_s}Saves{_s}SinglePlayer{_s}{campaignName}{_s}{fileName}", serializedJson);

        return true;
    }

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadGameFromFile))]
    [HarmonyPrefix]
    private static bool LoadGameFromFile(string loadFileName, OnLoadOrSaveCampaignFinishedCallback onLoadOrSaveCampaignFinishedCallback, SaveLoadManager __instance)
    {
        if (!File.Exists(loadFileName)) return true;

        var fileName = loadFileName.Substring(loadFileName.LastIndexOf(_s) + 1);
        var campaignName = Game.SaveLoadManager.ActiveCampaignFolderPath.Substring(Game.SaveLoadManager.ActiveCampaignFolderPath.LastIndexOf(_s) + 1);

        if (!File.Exists($"{PluginFolderPathStatic}{_s}Saves{_s}SinglePlayer{_s}{campaignName}{_s}{fileName}"))
        {
            GenerateTechs();
            return true;
        }
        Save deserializedJson = JsonConvert.DeserializeObject<Save>(File.ReadAllText($"{PluginFolderPathStatic}{_s}Saves{_s}SinglePlayer{_s}{campaignName}{_s}{fileName}"));
        foreach (var pair in _techsObtained.ToList()) _techsObtained[pair.Key] = false;
        foreach (var node in deserializedJson.UnlockedTechs) _techsObtained[node] = true;
        foreach (var situationOccurance in deserializedJson.SituationOccurances)
        {
            _situationOccurances[situationOccurance.BodyName][CraftSituation.Landed] = situationOccurance.Landed;
            _situationOccurances[situationOccurance.BodyName][CraftSituation.LowAtmosphere] = situationOccurance.LowAtmosphere;
            _situationOccurances[situationOccurance.BodyName][CraftSituation.HighAtmosphere] = situationOccurance.HighAtmosphere;
            _situationOccurances[situationOccurance.BodyName][CraftSituation.LowSpace] = situationOccurance.LowSpace;
            _situationOccurances[situationOccurance.BodyName][CraftSituation.HighSpace] = situationOccurance.HighSpace;
            _situationOccurances[situationOccurance.BodyName][CraftSituation.Orbit] = situationOccurance.Orbit;
        }
        _techPointBalance = deserializedJson.TechPointBalance;

        return true;
    }


    



    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        if (_disableMod) return;
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
            if (_windowTab == WindowTabs.TechTree)
            {
                _windowWidth = _techTreex2 + 650;
                _windowHeight = _techTreey2;
            }
            else if (_windowTab == WindowTabs.Goals)
            {
                _windowWidth = 560;
                _windowHeight = 575;
            }
            else if (_windowTab == WindowTabs.ModInfo)
            {
                _windowWidth = 500;
                _windowHeight = 500;
            }
            else
            {
                _logger.LogError("_windowTab is not set to a valid tab! If you're reading this, please report this bug!");
            }

            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "Hyperion Tech Tree",
                GUILayout.Height(_windowHeight),
                GUILayout.Width(_windowWidth)
            );
        }
    }

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private static void FillWindow(int windowID)
    {
        Color defaultColor = GUI.backgroundColor;
        GUILayout.Label("", GUILayout.Width(_windowWidth), GUILayout.Height(_windowHeight));        
        GUI.DrawTexture(new Rect(0, 0, 120, 10000), GetTextureFromColor(new Color(1f, 1f, 1f, 1f)));
        GUI.DrawTexture(new Rect(10, 10, 100, 25), GetTextureFromColor(new Color(0.25f, 0.25f, 0.25f, 1f)));

        GUIStyle style = new GUIStyle { alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(10, 10, 100, 25), _techPointBalance.ToString(), style);
        GUI.backgroundColor = (_windowTab == WindowTabs.TechTree) ? Color.yellow : Color.blue;
        if (GUI.Button(new Rect(10, 45, 100, 25), "Tech Tree")) _windowTab = WindowTabs.TechTree;
        GUI.backgroundColor = (_windowTab == WindowTabs.Goals) ? Color.yellow : Color.blue;
        if (GUI.Button(new Rect(10, 80, 100, 25), "Goals")) _windowTab = WindowTabs.Goals;
        GUI.backgroundColor = (_windowTab == WindowTabs.ModInfo) ? Color.yellow : Color.blue;
        if (GUI.Button(new Rect(10, 115, 100, 25), "Mod Info")) _windowTab = WindowTabs.ModInfo;
        
        GUI.backgroundColor = Color.red;
        if (GUI.Button(new Rect(_windowWidth - 10, 10, 20, 20), "X")) _isWindowOpen = false;

        switch (_windowTab)
        {
            case WindowTabs.TechTree:
                DrawTechTreeWindow();
                break;
            case WindowTabs.Goals:
                DrawGoalsWindow();
                break;
            case WindowTabs.ModInfo:
                DrawModInfoWindow(); 
                break;
            default:
                GUI.Label(new Rect(150, 150, 150, 150), "ERROR: Couldn't find tab!");
                _logger.LogError("Tried to open illegal tab!");
                break;
        }
        
        void DrawTechTreeWindow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_techTreex2 + 10);
            //GUILayout.Label("Test label");
            GUILayout.BeginVertical();
            GUILayout.Space(-_techTreey2);

            foreach (var node in _techTreeNodes)
            {
                foreach (var dependency in node.Dependencies)
                {
                    DrawLine(
                        node.PosX + (ButtonSize / 2) + (0.5 * LineWidth),
                        node.PosY + (ButtonSize / 2) + (0.5 * LineWidth),
                        _techTreeNodes.Find(x => x.NodeID == dependency).PosX + (ButtonSize / 2) + (0.5 * LineWidth),
                        _techTreeNodes.Find(x => x.NodeID == dependency).PosY + (ButtonSize / 2) + (0.5 * LineWidth)
                    );
                }
            }

            foreach (var node in _techTreeNodes)
            {

                if (_techsObtained[node.NodeID]) GUI.backgroundColor = Color.green;
                else
                {
                    GUI.backgroundColor = Color.blue;
                    int i = node.Dependencies.Count;
                    foreach (var dependency in node.Dependencies)
                    {
                        if (!_techsObtained[dependency])
                        {
                            i--;
                            if (node.RequiresAll)
                            {
                                GUI.backgroundColor = Color.red;
                            }
                            if (!node.RequiresAll && i == 0)
                            {
                                GUI.backgroundColor = Color.red;
                            }
                        }

                    }
                }
                if (_focusedNode != null) if (_focusedNode.NodeID == node.NodeID) GUI.backgroundColor = Color.white;

                Texture2D texture;
                if (File.Exists($"{_path}{_s}assets{_s}images{_s}{node.NodeID}.png")) texture = AssetManager.GetAsset<Texture2D>($"{_swPath}/images/{node.NodeID}.png");
                else
                {
                    // decomment this line once all icons are added
                    // probably also want to make it not be called every frame
                    //_logger.LogWarning($"Could not find button texture for node {node.NodeID}!");
                    texture = Texture2D.blackTexture;
                }

                if (GUI.Button(new Rect(node.PosX, node.PosY, ButtonSize, ButtonSize), texture)) _focusedNode = node;
            }
            GUI.backgroundColor = defaultColor;
            // GUILayout.Space(_techTreey2 + 10);
            if (_focusedNode != null)
            {
                //GUI.Label(new Rect(_techTreex2 + 10, _techTreey1, 600, 10000), $"Selected Node: {_focusedNode.NodeID}");
                GUILayout.Label($"Selected Node: {_focusedNode.NodeID}");

                string requirementPrefix;
                if (_focusedNode.Dependencies.Count > 1) requirementPrefix = _focusedNode.RequiresAll ? "Requires All: " : "Requires Any: ";
                else requirementPrefix = "Requires: ";

                string dependencyList = "";
                int i = _focusedNode.Dependencies.Count;
                foreach (var dependency in _focusedNode.Dependencies)
                {
                    dependencyList = String.Concat(dependencyList, dependency);
                    i--;
                    if (i > 0) dependencyList = String.Concat(dependencyList, ", ");
                }
                //GUI.Label(new Rect(_techTreex2 + 10, _techTreey1 + 20, 600, 10000), requirementPrefix + dependencyList);
                GUILayout.Label(requirementPrefix + dependencyList);

                string partList = "";
                i = _focusedNode.Parts.Count;
                foreach (var part in _focusedNode.Parts)
                {
                    LocalizedString partName = $"Parts/Title/{part}";
                    LocalizedString partSubtitle = $"Parts/Subtitle/{part}";
                    if (part == "fueltank_5v_inline_hydrogen_sphere") partList = $"{partList}HFT “Spherotron” Hydrogen Fuel Tank"; // As of KSP 0.1.1.0 this part doesn't have naming implemented correctly; this name is in the files but doesn't appear in-game 
                    else partList = $"{partList}{partName} {partSubtitle}";
                    i--;
                    if (i > 0) partList = $"{partList}, ";
                }
                //GUI.Label(new Rect(_techTreex2 + 10, _techTreey1 + 40, 600, 10000), "Unlocks Parts: " + partList);
                GUILayout.Label("Unlocks Parts: " + partList);

                if (_techsObtained[_focusedNode.NodeID])
                {
                    //GUI.Label(new Rect(_techTreex1, _techTreey2 + 10, 1000, 10000), "You already own this tech!");
                    GUILayout.Label("You already own this tech!");
                }
                else
                {
                    bool skipButtonFlag = false;
                    int j = _focusedNode.Dependencies.Count;
                    foreach (var dependency in _focusedNode.Dependencies)
                    {
                        if (!_techsObtained[dependency])
                        {
                            j--;
                            if (_focusedNode.RequiresAll)
                            {
                                skipButtonFlag = true;
                                //GUI.Label(new Rect(_techTreex1, _techTreey2 + 10 + 20 * (_focusedNode.Dependencies.Count - (j + 1)), 1000, 10000), $"Missing dependency {dependency}!");
                                GUILayout.Label($"Missing requirement tech: {dependency}!");
                            }
                            if (!_focusedNode.RequiresAll && j == 0)
                            {
                                skipButtonFlag = true;
                                //GUI.Label(new Rect(_techTreex1, _techTreey2 + 10, 1000, 10000), "Missing all dependencies!");
                                GUILayout.Label("Missing all requirement techs!");
                            }
                        }
                    }
                    if (!skipButtonFlag)
                    {
                        defaultColor = GUI.backgroundColor;
                        GUI.backgroundColor = _focusedNode.Cost > _techPointBalance ? Color.red : Color.blue;
                        if (GUILayout.Button(_focusedNode.Cost > _techPointBalance ? $"{_focusedNode.NodeID} is too expensive! Costs {_focusedNode.Cost} Tech Points (You have {_techPointBalance})" : $"Unlock {_focusedNode.NodeID}. Costs {_focusedNode.Cost} Tech Points (You have {_techPointBalance})"))
                        {
                            if (_focusedNode.Cost <= _techPointBalance)
                            {
                                _techsObtained[_focusedNode.NodeID] = true;
                                _techPointBalance -= _focusedNode.Cost;
                            }
                        }
                        GUI.backgroundColor = defaultColor;
                    }

                }
            }
            else
            {
                GUI.Label(new Rect(_techTreex2 + 10, _techTreey1, 300, 10000), "No nodes selected! Click on a node to bring up information!");
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        
        void DrawGoalsWindow()
        {
            GUI.backgroundColor = defaultColor;
            GUILayout.BeginHorizontal();
            GUILayout.Space(130);
            GUILayout.BeginVertical();
            GUILayout.Space(-_windowHeight);
            _scrollbarPos1 = GUILayout.BeginScrollView(_scrollbarPos1, false, false, GUILayout.Width(200), GUILayout.Height(500));
            foreach (var log in _sciradLog)
            {
                GUILayout.Label(log);
            }
            GUILayout.EndScrollView();
            GUI.DrawTexture(new Rect(140, 562, 419, 5), GetTextureFromColor(new Color(0f, 0f, 0f, 1f)));
            if (_remainingTime < ScienceSecondsOfDelay && _remainingTime > 0)
            {
                
                var roundedTime = Math.Round(_remainingTime, 1).ToString();
                if (roundedTime.Length == 1) roundedTime += ".0";

                GUI.Label(new Rect(149, 535, 400, 40), $"{roundedTime}s Remaining | {_awardAmount} Tech Points");
                GUI.DrawTexture(new Rect(140, 562, 419 - (419f / ScienceSecondsOfDelay * _remainingTime), 5), GetTextureFromColor(new Color(1f, 1f, 1f, 1f)));
            }
            else
            {
                GUI.Label(new Rect(149, 535, 400, 40), "Not currently doing science.");
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Space(-_windowHeight);
            _scrollbarPos2 = GUILayout.BeginScrollView(_scrollbarPos2, false, false, GUILayout.Width(200), GUILayout.Height(500));
            foreach (var goal in GoalsList)
            {
                if (!_collapsableList.ContainsKey(goal.BodyName)) _collapsableList.Add(goal.BodyName, false);
                GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
                if (GUILayout.Button(goal.BodyName)) _collapsableList[goal.BodyName] = !_collapsableList[goal.BodyName];
                GUI.backgroundColor = defaultColor;
                if (!_collapsableList[goal.BodyName]) continue;

                foreach (var name in CraftSituationSpacedShort)
                {
                    switch (Game.GlobalGameState.GetState())
                    {
                        default:
                            GUILayout.Label($"|-- {name.Value}: {_situationOccurances[goal.BodyName][name.Key]}");
                            break;
                        case GameState.FlightView:
                            GUILayout.Label($"|-- {name.Value}: {Checkmark(name.Key, goal.BodyName)} ({_situationOccurances[goal.BodyName][name.Key]})");
                            break;
                    }
                    //GUILayout.Label($"|-- {name.Value}: {Checkmark(CraftSituation.Landed)} ({_situationOccurances[goal.BodyName][CraftSituation.Landed]})");
                }


                //GUILayout.Label($"|-- Landed: {_situationOccurances[goal.BodyName][CraftSituation.Landed]} {Checkmark(CraftSituation.Landed)}");
                //GUILayout.Label($"|-- Low Atmo: {_situationOccurances[goal.BodyName][CraftSituation.LowAtmosphere]} {Checkmark(CraftSituation.LowAtmosphere)}");
                //GUILayout.Label($"|-- High Atmo: {_situationOccurances[goal.BodyName][CraftSituation.HighAtmosphere]} {Checkmark(CraftSituation.HighAtmosphere)}");
                //GUILayout.Label($"|-- Low Space: {_situationOccurances[goal.BodyName][CraftSituation.LowSpace]} {Checkmark(CraftSituation.LowSpace)}");
                //GUILayout.Label($"|-- High Space: {_situationOccurances[goal.BodyName][CraftSituation.HighSpace]} {Checkmark(CraftSituation.HighSpace)}");
                //GUILayout.Label($"|-- Orbit: {_situationOccurances[goal.BodyName][CraftSituation.Orbit]} {Checkmark(CraftSituation.Orbit)}");
                //foreach (var license in _kerbalLicenses)
                //{
                //    GUILayout.Label($"license debug data: 1| {license.Key}");
                //    foreach (var license2 in license.Value)
                //    {
                //        GUILayout.Label("2| " + license2.Key);
                //        foreach (var license3 in license2.Value)
                //        {
                //            GUILayout.Label("3| " + license3.ToString());
                //        }
                        
                //    }
                //}
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void DrawModInfoWindow()
        {
            GUILayout.Space(50);
            GUILayout.BeginHorizontal();
            GUILayout.Space(-_windowHeight + 200);
            GUILayout.BeginVertical();
            GUILayout.Label("MOD DEBUG INFO");
            _scrollbarPos3 = GUILayout.BeginScrollView(_scrollbarPos3, GUILayout.Width(400), GUILayout.Height(400));
            GUILayout.Label("-----");
            foreach (var license in _kerbalLicenses)
            {
                GUILayout.Label("Kerbal: " + license.Key);
                foreach (var body in license.Value)
                {
                    GUILayout.Label("Body name: " + body.Key);
                    foreach (var sit in body.Value)
                    {
                        GUILayout.Label("Sit: " + sit.ToString());
                    }
                }
                GUILayout.Label("-----");
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    /// <summary>
    /// A json reader method. Construct using a file path to the target .json file.
    /// </summary>
    /// <param name="jsonFilePath"></param>
    private void CreateJsonReader(string jsonFilePath)
    {
        _jsonFilePath = jsonFilePath;
        var jsonString = File.ReadAllText(_jsonFilePath);
        _jsonTechTree = JsonConvert.DeserializeObject<TechTree>(jsonString);
    }

    /// <summary>
    /// Finalizes the tech tree, reading all .json files in {PluginFolderPath}/Tech Tree and merges them. Any new parameters 
    /// </summary>
    private static void GenerateTechs()
    {
        // Loads DefaultTechTreeFilePath before any other json is loaded.
        // The current system for having duplicate nodes ignores everything except the parts list
        // of the dupe node, which is an issue if the node that declares all of the non-part data
        // is loaded as a duplicate node. Loading it in first fixes this issue.
        if (File.Exists(DefaultTechTreeFilePath)) Generate(DefaultTechTreeFilePath);
        foreach (string file in Directory.GetFiles($"{PluginFolderPathStatic}{_s}Tech Tree"))
        {
            if (file != DefaultTechTreeFilePath) Generate(file);
        }

        void Generate(string file)
        {
            _logger.LogInfo($"Found tech tree! {file}");
            _jsonFilePath = file;
            string jsonString;

            jsonString = File.ReadAllText(_jsonFilePath);
            _jsonTechTree = JsonConvert.DeserializeObject<TechTree>(jsonString);
            foreach (var node in _jsonTechTree.Nodes)
            {

                if (_techTreeNodes.Exists(x => x.NodeID == node.NodeID))
                {
                    _logger.LogInfo($"Found multiple nodes with ID {node.NodeID}! Attempting merge.");
                    var originalNode = _techTreeNodes.Find(x => x.NodeID == node.NodeID);
                    foreach (string part in node.Parts) if (!originalNode.Parts.Contains(part)) originalNode.Parts.Add(part);
                    continue;
                }
                _techTreeNodes.Add(node);
                _techsObtained.Add(node.NodeID, node.UnlockedInitially);
                node.PosX += 115;
                if (node.PosX < _techTreex1) _techTreex1 = node.PosX;
                if (node.PosX > _techTreex2) _techTreex2 = node.PosX + ButtonSize;
                if (node.PosY < _techTreey1) _techTreey1 = node.PosY;
                if (node.PosY > _techTreex2) _techTreey2 = node.PosY + ButtonSize;
            }
            if (_jsonTechTree.ModVersion != MyPluginInfo.PLUGIN_VERSION)
            {
                _logger.LogWarning($"Version mismatch between mod version and the version of {file}!");
                _logger.LogWarning($"Mod Version: {MyPluginInfo.PLUGIN_VERSION} | Tech Tree Version: {_jsonTechTree.ModVersion}");
            }
        }
    }

    private void GenerateGoals()
    {
        if (File.Exists(DefaultGoalsFilePath)) Generate(DefaultGoalsFilePath);
        foreach (string file in Directory.GetFiles($"{PluginFolderPath}{_s}Goals"))
        {
            if (file != DefaultGoalsFilePath) Generate(file);
        }

        void Generate(string file)
        {
            _logger.LogInfo($"Found goals file! {file}");
            _jsonFilePath = file;
            var jsonString = File.ReadAllText(_jsonFilePath);
            _jsonGoals = JsonConvert.DeserializeObject<Goals>(jsonString);
            foreach (var goal in _jsonGoals.Bodies)
            {

                if (GoalsList.Exists(x => x.BodyName == goal.BodyName))
                {
                    _logger.LogError($"Found multiple goal files for celestial body {goal.BodyName}! Current build doesn't support goal merging!");
                    continue;
                }
                GoalsList.Add(goal);


            }
            if (_jsonGoals.ModVersion != MyPluginInfo.PLUGIN_VERSION)
            {
                _logger.LogWarning($"Version mismatch between mod version and the version of {file}!");
                _logger.LogWarning($"Mod Version: {MyPluginInfo.PLUGIN_VERSION} | Goal Version: {_jsonGoals.ModVersion}");
            }
        }
    }


    

        // Parts of the next two methods are copy-pasted from VChristof's InteractiveFilter mod
        [HarmonyPatch(typeof(AssemblyFilterContainer), nameof(AssemblyFilterContainer.AddButton))]
    class PatchAssemblyFilterContainer
    {
        [HarmonyPostfix]
        static void AddButton(ref GameObject btn)
        {
            try
            {
                PartHider(btn.GetComponent<AssemblyPartsButton>());
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }
    }

    private static void PartHider(AssemblyPartsButton assemblyPartsButton)
    {
        if (!_assemblyPartsButtons.Contains(assemblyPartsButton))
        {
            _assemblyPartsButtons.Add(assemblyPartsButton);
        }
        assemblyPartsButton.gameObject.SetActive(false);
        foreach (var node in _techTreeNodes)
        {
            if (!_techsObtained[node.NodeID]) continue;
            foreach (var part in node.Parts)
            {
                if (part == assemblyPartsButton.part.Name)
                {
                    assemblyPartsButton.gameObject.SetActive(true);
                }
            }
        }
    }

    /// <summary>
    /// Draws a line between two points on the UI.
    /// </summary>
    /// <param name="x1"></param>
    /// <param name="y1"></param>
    /// <param name="x2"></param>
    /// <param name="y2"></param>
    private static void DrawLine(double x1, double y1, double x2, double y2)
    {
        // trig time!
        double adjacent = x2 - x1;
        double opposite = y2 - y1;

        double length = Math.Sqrt(Math.Pow(adjacent, 2) + Math.Pow(opposite, 2));
        GUI.backgroundColor = Color.white;
        GUIUtility.RotateAroundPivot((float)((Math.Atan(opposite / adjacent) * (180 / Math.PI)) + 180), new Vector2((float)x1, (float)y1));
        GUI.DrawTexture(new Rect((float)x1, (float)y1, (float)length, LineWidth), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot((float)-((Math.Atan(opposite / adjacent) * (180 / Math.PI)) + 180), new Vector2((float)x1, (float)y1));
    }

    /// <summary>
    /// Generates a 1x1 texture with a specified color
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    private static Texture2D GetTextureFromColor(Color color)
    {
        _tex.SetPixel(1, 1, color);
        _tex.Apply();
        return _tex;
    }

    private static string GetHumanReadableUT(double? nullableUT)
    {
        if (nullableUT.HasValue)
        {
            var ut = nullableUT.Value;
            var seconds = Math.Floor(ut % 60).ToString();
            var minutes = Math.Floor(ut / 60 % 60).ToString();
            var hours = Math.Floor(ut / (60 * 60) % 6).ToString();
            var days = Math.Floor(ut / (60 * 60 * 6) % 425).ToString();
            var years = Math.Floor(ut / (60 * 60 * 6 * 425)).ToString();

            if (seconds.Length == 1) seconds = "0" + seconds;
            if (minutes.Length == 1) minutes = "0" + minutes;
            hours = "0" + hours;
            if (days.Length == 1) days = "00" + days;
            else if (days.Length == 2) days = "0" + days;

            return $"{years}y, {days}d, {hours}:{minutes}:{seconds}";
        }
        else
        {
            return "invalid";
        }
        
    }
}