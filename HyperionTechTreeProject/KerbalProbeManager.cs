using KSP.Game;
using SpaceWarp.API.Game;
using KSP.Sim.impl;
using BepInEx.Logging;
using BepInEx;
using Newtonsoft.Json;
using static HyperionTechTree.HyperionTechTreePlugin;

namespace HyperionTechTree;

/// <summary>
/// Abstractions for code that handle kerbal and probes.
/// </summary>
public class KerbalProbeManager
{
    private static GameInstance Game;
    private static ManualLogSource _logger;
    private static readonly char _s = System.IO.Path.DirectorySeparatorChar;
    
    

    internal static VesselComponent SimVessel { get; private set; }
    internal static VesselVehicle VesselVehicle { get; private set; }

    private static List<string> _ppdCrewed = new(), _ppdUncrewed = new();
    private static List<GoalsBody> _goals = new();

    private static PartComponentModule_Command _module = new();
    private static List<KerbalInfo> _kerfo = new();

    // PRIVATE THIS LATER
    internal static Dictionary<string, Dictionary<string, List<CraftSituation>>> _kerbalLicenses = new();

    private static Dictionary<string, Dictionary<string, List<CraftSituation>>> _probeLicenses = new();

    private static List<PartComponent> _allPartsInVessel = new();

    /// <summary>
    /// "Constructs" KPM, despite it being static
    /// </summary>
    public static void KerbalProbeManagerInitialize()
    {
        Game = GameManager.Instance.Game;
        _logger = HyperionTechTreePlugin.HLogger;
        UpdateKPM();
    }

    /// <summary>
    /// Runs every "update" method in KPM
    /// </summary>
    public static void UpdateKPM()
    {
        UpdateVessels();
        UpdateParts();
        UpdateGoals();
        UpdateKerfo();
        
    }

    /// <summary>
    /// Sets _simVessel and _vesselVehicle to the active vessel.
    /// </summary>
    /// <returns>Bool for if the update was successful. (this hides null reference exceptions)</returns>
    private static bool UpdateVessels()
    {
        try
        {
            SimVessel = Vehicle.ActiveSimVessel;
            VesselVehicle = Vehicle.ActiveVesselVehicle;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool UpdateKerfo()
    {
        try
        {
            _kerfo = new();
            foreach (var part in _allPartsInVessel)
            {
                _kerfo.AddRange(Game.KerbalManager._kerbalRosterManager.GetAllKerbalsInSimObject(part.GlobalId));
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }

    }

    private static bool UpdateParts()
    {
        try
        {
            _allPartsInVessel = SimVessel.GetControlOwner()._partOwner._parts.PartsEnumerable.ToList();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    private static void UpdateGoals()
    {
        _goals = GoalsList;
    }


    /// <summary>
    /// Creates an empty _situationOccurances
    /// </summary>
    public static void GenerateSituationOccurances()
    {
        var goals = HyperionTechTreePlugin.GoalsList;
        foreach (var goal in goals)
        {
            _situationOccurances.Add(goal.BodyName, new()
            {
                { CraftSituation.Landed, 0 },
                { CraftSituation.LowAtmosphere, 0 },
                { CraftSituation.HighAtmosphere, 0 },
                { CraftSituation.LowSpace, 0 },
                { CraftSituation.HighSpace, 0 },
                { CraftSituation.Orbit, 0 },
            });
        }
    }

    public static void GeneratePPD()
    {
        var ppd = JsonConvert.DeserializeObject<PodProbeDistinction>(File.ReadAllText($"{HyperionTechTreePlugin.Path}{_s}PodProbeDistinction{_s}DefaultPodProbeDistinction.json"));
        _ppdCrewed = ppd.Crewed;
        _ppdUncrewed = ppd.Uncrewed;
    }

    /// <summary>
    /// Instantiates licenses for kerbals and probes.
    /// </summary>
    public static void GenerateLicenses()
    {
        foreach (var part in _allPartsInVessel)
        {
            foreach (var kerbal in _kerfo)
            {
                if (!_kerbalLicenses.ContainsKey(kerbal.Id.ToString()))
                {
                    InstantiateController(kerbal.Id.ToString(), true);
                    //_kerbalLicenses.Add(kerbal.Id.ToString(), new());
                }
            }

            if (_kerfo.Count < 1 && _ppdUncrewed.Contains(part.PartName))
            {
                if (!_probeLicenses.ContainsKey(part.GlobalId.ToString()))
                {
                    InstantiateController(part.GlobalId.ToString(), false);
                    //_probeLicenses.Add(part.GlobalId.ToString(), new());
                }
            }

        }
    }

    /// <summary>
    /// Takes a kerbal or probe's ID and creates a no-restriction license using it.
    /// </summary>
    /// <param name="guid">Kerbal "Id" or Probe part's "GlobalId"</param>
    /// <param name="isKerbal"><code>true</code> for kerbals, <code>false</code> for probes.</param>
    /// <returns><code>true</code> if the license was created, <code>false</code> if not.</returns>
    private static bool InstantiateController(string guid, bool isKerbal)
    {

        if (SimVessel == null)
        {
            return false;
        }

        if (isKerbal)
        {
            if (_kerbalLicenses.ContainsKey(guid)) return false;

            _kerbalLicenses[guid] = new();
            _logger.LogInfo($"Created key for {guid}");

            if (_kerbalLicenses[guid].ContainsKey(SimVessel.mainBody.Name)) return false;
            //_kerbalLicenses[guid][_simVessel.mainBody.Name].Add(_craftSituation);

            foreach (var body in _goals)
            {
                _kerbalLicenses[guid][body.BodyName] = new();
            }

        }
        else
        {
            if (_probeLicenses.ContainsKey(guid)) return false;
            _probeLicenses[guid] = new();
            foreach (var body in _goals)
            {
                _probeLicenses[guid][body.BodyName] = new();
            }
        }
        return true;
    }

    public static void AddSituationToLicense()
    {
        foreach (var kerbal in _kerfo)
        {
            foreach (var part in _allPartsInVessel)
            {
                if (kerbal.Location.SimObjectId == part.GlobalId)
                {
                    if (_orbitScienceFlag) 
                    {
                        _logger.LogInfo($"Situation added to license!\nID: {part.GlobalId}\nKerbal Name: {kerbal.NameKey}\nSituation: Orbit");
                        _kerbalLicenses[kerbal.Id.ToString()][SimVessel.mainBody.Name].Add(CraftSituation.Orbit);
                    }
                    else
                    {
                        if (_kerbalLicenses[kerbal.Id.ToString()][SimVessel.mainBody.Name].Contains(_craftSituation))
                        {
                            _logger.LogWarning($"Situation {_craftSituation} already in license of {kerbal.Id}");
                        }
                        else
                        {
                            _logger.LogInfo($"Situation added to license!\nID: {part.GlobalId}\nKerbal Name: {kerbal.NameKey}\nSituation: {_craftSituation}");
                            _kerbalLicenses[kerbal.Id.ToString()][SimVessel.mainBody.Name].Add(_craftSituation);
                        }
                    }
                }
            }
        }
    }

    internal static bool CheckSituationClaimed(CraftSituation sit)
    {
        foreach (var kerbal in _kerbalLicenses)
        {
            foreach (var celes in kerbal.Value)
            {
                if (_kerbalLicenses[kerbal.Key][celes.Key].Contains(sit))
                {
                    return true;
                }
            }
        }
        return false;
    }


    internal static char Checkmark(CraftSituation sit, string currentBody)
    {
        if (Game?.GlobalGameState?.GetState() != GameState.FlightView) return ' ';
            foreach (var part in SimVessel.GetControlOwner()._partOwner._parts.PartsEnumerable)
                foreach (var kerbal in Game.KerbalManager._kerbalRosterManager.GetAllKerbalsInSimObject(part.GlobalId))
                    if (kerbal.Location.SimObjectId == part.GlobalId)
                        if (_kerbalLicenses[kerbal.Id.ToString()][currentBody].Contains(sit))
                            return '✓';
        return 'X';
    }
}