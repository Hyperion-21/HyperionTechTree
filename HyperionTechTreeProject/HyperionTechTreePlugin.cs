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

    private const string ToolbarFlightButtonID = "BTN-HyperionTechTreeFlight";
    private const string ToolbarOABButtonID = "BTN-HyperionTechTreeOAB";

    private static string _jsonFilePath;
    private static TechTree _jsonObject;

    private static Dictionary<string, bool> _techsObtained = new();
    private static List<TechTreeNode> _techTreeNodes = new();
    private static int _techPointBalance = 500;

    private static TechTreeNode _focusedNode = null;

    private static List<AssemblyPartsButton> _assemblyPartsButtons = new();

    // Whenever typing a file path, use {_s} instead of / or \ or \\ unless the function specifically needs /
    // If in doubt, use {_s}, and if it causes a crash replace with / (NOT \ OR \\)
    // {_s} looks better when printed to log, "abc/def\ghi" vs "abc\def\ghi"
    // Some things that look like filepaths actually aren't (i.e. AssetManager.GetAsset calls),
    // and using \, \\, or {_s} there can cause crashes!
    // (because of course the difference between / and \ becomes relevant in this code)
    private static readonly char _s = Path.DirectorySeparatorChar;
    private string DefaultTechTreeFilePath => $"{PluginFolderPath}{_s}Tech Tree{_s}DefaultTechTree.json";
    private static string _path;
    private static string _swPath;

    private static ManualLogSource _logger;

    public static HyperionTechTreePlugin Instance { get; set; }

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();
        _logger = Logger;
        _path = PluginFolderPath;
        _swPath = SpaceWarpMetadata.ModID;
        _logger.LogInfo(_s);
        _logger.LogInfo($"{PluginFolderPath}{_s}swinfo.json");
        _logger.LogInfo(File.Exists($"{PluginFolderPath}{_s}swinfo.json").ToString());

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

        GenerateTechs();
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
            _windowWidth = _techTreex2 + 150; // For some reason, making the horizontal margin smaller messes with line rendering
            _windowHeight = _techTreey2 + 100;
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
                //_logger.LogWarning($"Could not find button texture for node {node.NodeID}!");
                texture = Texture2D.blackTexture;
            }



            if (GUI.Button(new Rect(node.PosX, node.PosY, ButtonSize, ButtonSize), texture))
            {
                _focusedNode = node;
            }
        }
        GUI.backgroundColor = defaultColor;
        GUILayout.Space(_techTreey2 + 50);
        if (_focusedNode != null) 
        {
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
            GUILayout.Label(requirementPrefix + dependencyList);

            string partList = "";
            i = _focusedNode.Parts.Count;
            foreach (var part in _focusedNode.Parts)
            {
                LocalizedString partName = $"Parts/Title/{part}";
                partList = String.Concat(partList, partName.ToString());
                i--;
                if (i > 0) partList = String.Concat(partList, ", ");
            }
            GUILayout.Label("Unlocks Parts: " + partList);

            if (_techsObtained[_focusedNode.NodeID])
            {
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
                            GUILayout.Label($"Missing dependency {dependency}!");
                        }
                        if (!_focusedNode.RequiresAll && j == 0)
                        {
                            skipButtonFlag = true;
                            GUILayout.Label("Missing all dependencies!");
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
            GUILayout.Label("No nodes selected! Click on a node to bring up information!");
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
        _jsonObject = JsonConvert.DeserializeObject<TechTree>(jsonString);
    }

    /// <summary>
    /// Finalizes the tech tree, reading all .json files in {PluginFolderPath}/Tech Tree and merges them. Any new parameters 
    /// </summary>
    private void GenerateTechs()
    {
        if (File.Exists(DefaultTechTreeFilePath)) GenerateNode(DefaultTechTreeFilePath);
        foreach (string file in Directory.GetFiles($"{PluginFolderPath}{_s}Tech Tree"))
        {
            if (file != DefaultTechTreeFilePath) GenerateNode(file);
        }

        void GenerateNode(string file)
        {
            _logger.LogInfo($"Found tech tree! {file}");
            CreateJsonReader(file);
            foreach (var node in _jsonObject.Nodes)
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
                if (node.PosX < _techTreex1) _techTreex1 = node.PosX;
                if (node.PosX > _techTreex2) _techTreex2 = node.PosX;
                if (node.PosY < _techTreey1) _techTreey1 = node.PosY;
                if (node.PosY > _techTreex2) _techTreey2 = node.PosY;
            }
            _logger.LogInfo($"Tree Dimensions: ({_techTreex1}, {_techTreey1}), ({_techTreex2}, {_techTreey2})");
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class TechTree
    {
        [JsonProperty("modVersion")] public string ModVersion { get; set; }
        [JsonProperty("nodes")] public List<TechTreeNode> Nodes { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TechTreeNode
    {
        [JsonProperty("nodeID")] public string NodeID { get; set; }
        [JsonProperty("dependencies")] public List<string> Dependencies { get; set; }
        [JsonProperty("requiresAll")] public bool RequiresAll { get; set; }
        [JsonProperty("posx")] public float PosX { get; set; }
        [JsonProperty("posy")] public float PosY { get; set; }
        [JsonProperty("parts")] public List<string> Parts { get; set; }
        [JsonProperty("cost")] public int Cost { get; set; }
        [JsonProperty("unlockedInitially")] public bool UnlockedInitially { get; set; }
    }

    // Parts of this code are copy-pasted from VChristof's InteractiveFilter mod
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
                Debug.LogError(e.Message);
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