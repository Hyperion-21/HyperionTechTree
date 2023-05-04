using Newtonsoft.Json;

namespace HyperionTechTree;

public static class JsonHandler
{

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
    [JsonProperty("cost")] public float Cost { get; set; }
    [JsonProperty("unlockedInitially")] public bool UnlockedInitially { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class Goals
{
    [JsonProperty("modVersion")] public string ModVersion { get; set; }
    [JsonProperty("bodies")] public List<GoalsBody> Bodies { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class GoalsBody
{
    [JsonProperty("bodyName")] public string BodyName { get; set; }
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
public class Save
{
    [JsonProperty("modVersion")] public string ModVersion { get; set; }
    [JsonProperty("techPointBalance")] public float TechPointBalance { get; set; }
    [JsonProperty("unlockedTechs")] public List<string> UnlockedTechs { get; set; }
    [JsonProperty("situationOccurances")] public List<SituationOccurance> SituationOccurances { get; set; }
    [JsonProperty("kerbalLicenses")] public List<License> KerbalLicenses { get; set; }
    [JsonProperty("probeLicenses")] public List<License> ProbeLicenses { get; set; }
    [JsonProperty("activeVesselSituation")] public string ActiveVesselSituation { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class SituationOccurance
{
    [JsonProperty("bodyName")] public string BodyName { get; set; }
    [JsonProperty("landed")] public int Landed { get; set; }
    [JsonProperty("lowAtmosphere")] public int LowAtmosphere { get; set; }
    [JsonProperty("highAtmosphere")] public int HighAtmosphere { get; set; }
    [JsonProperty("lowSpace")] public int LowSpace { get; set; }
    [JsonProperty("highSpace")] public int HighSpace { get; set; }
    [JsonProperty("orbit")] public int Orbit { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class License
{
    [JsonProperty("ID")] public string ID { get; set; }
    [JsonProperty("bodies")] public List<LicenseBody> Bodies { get; set; }
}

public class LicenseBody
{
    [JsonProperty("bodyName")] public string BodyName { get; set; }
    [JsonProperty("landed")] public bool Landed { get; set; }
    [JsonProperty("lowAtmosphere")] public bool LowAtmosphere { get; set; }
    [JsonProperty("highAtmosphere")] public bool HighAtmosphere { get; set; }
    [JsonProperty("lowSpace")] public bool LowSpace { get; set; }
    [JsonProperty("highSpace")] public bool HighSpace { get; set; }
    [JsonProperty("orbit")] public bool Orbit { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class PodProbeDistinction
{
    [JsonProperty("modVersion")] public string ModVersion { get; set; }
    [JsonProperty("crewed")] public List<string> Crewed { get; set; }
    [JsonProperty("uncrewed")] public List<string> Uncrewed { get; set; }
}