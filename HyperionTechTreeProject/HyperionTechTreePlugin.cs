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
    private Rect _windowRect = new(0, 0, _windowWidth, _windowHeight);
    private static float _windowHeight = 1;
    private static float _windowWidth = 1;
    private const float ButtonSize = 36;
    private const float LineWidth = 2;
    private static float _techTreex1 = 10000;
    private static float _techTreex2 = -10000;
    private static float _techTreey1 = 10000;
    private static float _techTreey2 = -10000;
    private enum WindowTabs
    {
        TechTree,
        Goals
    }
    private static WindowTabs _windowTab = WindowTabs.TechTree;

    private const float ScienceSecondsOfDelay = 10;
    private float _remainingTime = float.MaxValue;
    private enum CraftSituation
    {
        Landed,
        Splashed,
        LowAtmosphere,
        HighAtmosphere,
        LowSpace,
        HighSpace
    }
    private static CraftSituation _craftSituation = CraftSituation.Landed;
    private static CraftSituation _craftSituationOld = CraftSituation.Landed;

    private const string ToolbarFlightButtonID = "BTN-HyperionTechTreeFlight";
    private const string ToolbarOABButtonID = "BTN-HyperionTechTreeOAB";

    private static string _jsonFilePath;
    private static TechTree _jsonTechTree;
    private static Goals _jsonGoals;

    private static Dictionary<string, bool> _techsObtained = new();
    private static List<TechTreeNode> _techTreeNodes = new();
    private static List<GoalsBody> _goals = new();
    private static float _techPointBalance = 100000;

    private static TechTreeNode _focusedNode = null;

    private static List<AssemblyPartsButton> _assemblyPartsButtons = new();

    // Whenever typing a file path, use {_s} instead of / or \ or \\ unless the function specifically needs /
    // If in doubt, use {_s}, and if it causes a crash replace with / (NOT \ OR \\)
    // {_s} looks better when printed to log, "abc/def\ghi" vs "abc\def\ghi"
    // Some things that look like filepaths actually aren't (i.e. AssetManager.GetAsset calls),
    // and using \, \\, or {_s} there can cause crashes!
    // (because of course the difference between / and \ becomes relevant in this code)
    private static readonly char _s = Path.DirectorySeparatorChar;
    private static string PluginFolderPathStatic;
    private static string DefaultTechTreeFilePath => $"{PluginFolderPathStatic}{_s}Tech Tree{_s}DefaultTechTree.json";
    private static string DefaultGoalsFilePath => $"{PluginFolderPathStatic}{_s}Goals{_s}DefaultGoals.json";
    private static string _path;
    private static string _swPath;

    private static ManualLogSource _logger;
    private static bool _disableMod = false;

    public static HyperionTechTreePlugin Instance { get; set; }

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();
        _logger = Logger;
        PluginFolderPathStatic = PluginFolderPath;

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

        foreach (var goal in _goals)
        {
            _logger.LogInfo($"Body Name: {goal.BodyName}");
            _logger.LogInfo($"Atmosphere Threshold: {goal.AtmosphereThreshold}");
        }
    }

    private void Update()
    {
        _remainingTime -= Time.deltaTime;
        if (_remainingTime <= ScienceSecondsOfDelay) _logger.LogInfo(_remainingTime.ToString());



        if (Game.GlobalGameState.GetState() != GameState.FlightView) return;
        var simVessel = Vehicle.ActiveSimVessel;
        var vesselVehicle = Vehicle.ActiveVesselVehicle;
        float awardAmount = 0;
        foreach (var body in _goals)
        {
            if (body.BodyName != simVessel.mainBody.bodyName) continue;

            if ((int)simVessel.Situation <= 2 && simVessel.Situation != KSP.Sim.impl.VesselSituations.Splashed)
            {
                _craftSituation = CraftSituation.Landed;
                awardAmount = body.LandedAward;
                //_logger.LogInfo("Craft is landed!");
            }
            else if (simVessel.Situation == KSP.Sim.impl.VesselSituations.Splashed)
            {
                _craftSituation = CraftSituation.Splashed;
                awardAmount = body.LandedAward;
                //_logger.LogInfo("Craft is splashed!");
            }
            else if (simVessel.IsInAtmosphere && simVessel.AltitudeFromSeaLevel < body.AtmosphereThreshold && (int)simVessel.Situation > 2)
            {
                _craftSituation = CraftSituation.LowAtmosphere;
                awardAmount = body.LowAtmosphereAward;
                //_logger.LogInfo("Craft is flying in low atmosphere!");
            }
            else if (simVessel.IsInAtmosphere && simVessel.AltitudeFromSeaLevel >= body.AtmosphereThreshold)
            {
                _craftSituation = CraftSituation.HighAtmosphere;
                awardAmount = body.HighAtmosphereAward;
                //_logger.LogInfo("Craft is flying in high atmosphere!");
            }
            else if (!simVessel.IsInAtmosphere && simVessel.AltitudeFromSeaLevel < body.SpaceThreshold)
            {
                _craftSituation = CraftSituation.LowSpace;
                awardAmount = body.LowSpaceAward;
                //_logger.LogInfo("Craft is flying in low space!");
            }
            else if (!simVessel.IsInAtmosphere && simVessel.AltitudeFromSeaLevel >= body.SpaceThreshold)
            {
                _craftSituation = CraftSituation.HighSpace;
                awardAmount = body.HighSpaceAward;
                //_logger.LogInfo("Craft is flying in high space!");
            }

            if (_remainingTime < 0)
            {
                _techPointBalance += awardAmount;
                _logger.LogInfo($"Science successfully performed! Gained {awardAmount} tech points!");
                _remainingTime = float.MaxValue;
            }

            if (_craftSituation != _craftSituationOld)
            {
                _remainingTime = ScienceSecondsOfDelay;
                _logger.LogInfo($"Craft changing states! Going from {_craftSituationOld} to {_craftSituation}! Maintain the current state for {ScienceSecondsOfDelay} to gain {awardAmount} tech points!");
                _craftSituationOld = _craftSituation;
            }
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

        SituationOccurance situationOccurance = new SituationOccurance();
        situationOccurance.BodyName = "Dummy Planet";
        situationOccurance.Landed = 0;
        situationOccurance.LowAtmosphere = 0;
        situationOccurance.HighAtmosphere = 0;
        situationOccurance.LowSpace = 0;
        situationOccurance.HighSpace = 0;
        situationOccurance.Orbit = 0;

        save.SituationOccurances = new() { situationOccurance };

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

        _logger.LogInfo(fileName);
        Save deserializedJson = JsonConvert.DeserializeObject<Save>(File.ReadAllText($"{PluginFolderPathStatic}{_s}Saves{_s}SinglePlayer{_s}{campaignName}{_s}{fileName}"));
        foreach (var pair in _techsObtained.ToList()) _techsObtained[pair.Key] = false;
        foreach (var node in deserializedJson.UnlockedTechs) _techsObtained[node] = true;
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
                _windowHeight = _techTreey2 + 100;
            }
            else
            {
                _windowWidth = 200;
                _windowHeight = 200;
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

        Texture2D tex = new(1, 1);
        //double secondsInCurrentSecond = ((DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds % 1000)  / 1000;
        //_logger.LogInfo(secondsInCurrentSecond);
        //Color sidebarColor = new((float)secondsInCurrentSecond, (float)secondsInCurrentSecond, (float)secondsInCurrentSecond);
        //_logger.LogInfo(sidebarColor.ToString());
        //tex.SetPixel(0, 0, sidebarColor);
        GUI.DrawTexture(new Rect(0, 0, 120, 10000), tex);
        GUI.backgroundColor = (_windowTab == WindowTabs.TechTree) ? Color.yellow : Color.blue;
        if (GUI.Button(new Rect(10, 10, 100, 25), "Tech Tree")) _windowTab = WindowTabs.TechTree;
        GUI.backgroundColor = (_windowTab == WindowTabs.Goals) ? Color.yellow : Color.blue;
        if (GUI.Button(new Rect(10, 45, 100, 25), "Goals")) _windowTab = WindowTabs.Goals;

        if (_windowTab == WindowTabs.TechTree)
        {
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
                GUI.Label(new Rect(_techTreex2 + 10, _techTreey1, 600, 10000), $"Selected Node: {_focusedNode.NodeID}");

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
                GUI.Label(new Rect(_techTreex2 + 10, _techTreey1 + 20, 600, 10000), requirementPrefix + dependencyList);

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
                GUI.Label(new Rect(_techTreex2 + 10, _techTreey1 + 40, 600, 10000), "Unlocks Parts: " + partList);

                if (_techsObtained[_focusedNode.NodeID])
                {
                    GUI.Label(new Rect(_techTreex1, _techTreey2 + 10, 1000, 10000), "You already own this tech!");
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
                                GUI.Label(new Rect(_techTreex1, _techTreey2 + 10 + 20 * (_focusedNode.Dependencies.Count - (j + 1)), 1000, 10000), $"Missing dependency {dependency}!");
                            }
                            if (!_focusedNode.RequiresAll && j == 0)
                            {
                                skipButtonFlag = true;
                                GUI.Label(new Rect(_techTreex1, _techTreey2 + 10, 1000, 10000), "Missing all dependencies!");
                            }
                        }
                    }
                    if (!skipButtonFlag)
                    {
                        defaultColor = GUI.backgroundColor;
                        GUI.backgroundColor = _focusedNode.Cost > _techPointBalance ? Color.red : Color.blue;
                        if (GUILayout.Button(_focusedNode.Cost > _techPointBalance ? $"Tech Is Too Expensive! Costs {_focusedNode.Cost} Tech Points (You Have {_techPointBalance})" : $"Unlock Tech. Costs {_focusedNode.Cost} Tech Points (You Have {_techPointBalance})"))
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
        }
        else if (_windowTab == WindowTabs.Goals)
        {
            GUILayout.Label("Heyo");
        }

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

                if (_goals.Exists(x => x.BodyName == goal.BodyName))
                {
                    _logger.LogError($"Found multiple goal files for celestial body {goal.BodyName}! Current build doesn't support goal merging!");
                    continue;
                }
                _goals.Add(goal);
            }
            if (_jsonGoals.ModVersion != MyPluginInfo.PLUGIN_VERSION)
            {
                _logger.LogWarning($"Version mismatch between mod version and the version of {file}!");
                _logger.LogWarning($"Mod Version: {MyPluginInfo.PLUGIN_VERSION} | Goal Version: {_jsonGoals.ModVersion}");
            }
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    private class TechTree
    {
        [JsonProperty("modVersion")] public string ModVersion { get; set; }
        [JsonProperty("nodes")] public List<TechTreeNode> Nodes { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    private class TechTreeNode
    {
        [JsonProperty("nodeID")] public string NodeID { get; set; }
        [JsonProperty("dependencies")] public List<string> Dependencies { get; set; }
        [JsonProperty("requiresAll")] public bool RequiresAll { get; set; }
        [JsonProperty("posx")] public float PosX { get; set; }
        [JsonProperty("posy")] public float PosY { get; set; }
        [JsonProperty("parts")] public List<string> Parts { get; set; }
        [JsonProperty("cost")] public float Cost { get; set; }
        [JsonProperty("unlockedInitially")] public bool UnlockedInitially { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    private class Goals
    {
        [JsonProperty("modVersion")] public string ModVersion { get; set; }
        [JsonProperty("bodies")] public List<GoalsBody> Bodies { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    private class GoalsBody
    {
        [JsonProperty("bodyName")] public string BodyName { get; set; }
        [JsonProperty("referenceBody")] public string ReferenceBody { get; set; }
        [JsonProperty("spaceThreshold")] public double SpaceThreshold { get; set; }
        [JsonProperty("atmosphereThreshold")] public double AtmosphereThreshold { get; set; }
        [JsonProperty("hasAtmosphere")] public bool HasAtmosphere { get; set; }
        [JsonProperty("hasSurface")] public bool HasSurface { get; set; }
        [JsonProperty("highSpaceAward")] public float HighSpaceAward { get; set; }
        [JsonProperty("lowSpaceAward")] public float LowSpaceAward { get; set; }
        [JsonProperty("orbitAward")] public float OrbitAward { get; set; }
        [JsonProperty("highAtmosphereAward")] public float HighAtmosphereAward { get; set; }
        [JsonProperty("lowAtmosphereAward")] public float LowAtmosphereAward { get; set; }
        [JsonProperty("landedAward")] public float LandedAward { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    private class Save
    {
        [JsonProperty("modVersion")] public string ModVersion { get; set; }
        [JsonProperty("techPointBalance")] public float TechPointBalance { get; set; }
        [JsonProperty("unlockedTechs")] public List<string> UnlockedTechs { get; set; }
        [JsonProperty("situationOccurances")] public List<SituationOccurance> SituationOccurances { get; set; }


    }

    private class SituationOccurance
    {
        [JsonProperty("bodyName")] public string BodyName { get; set; }
        [JsonProperty("landed")] public int Landed { get; set; }
        [JsonProperty("lowAtmosphere")] public int LowAtmosphere { get; set; }
        [JsonProperty("highAtmosphere")] public int HighAtmosphere { get; set; }
        [JsonProperty("lowSpace")] public int LowSpace { get; set; }
        [JsonProperty("highSpace")] public int HighSpace { get; set; }
        [JsonProperty("orbit")] public int Orbit { get; set; }
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
}