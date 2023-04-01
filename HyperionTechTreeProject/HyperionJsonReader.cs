/*

using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;

namespace HyperionTechTree;

public class HyperionJsonReader
{
    private string _jsonFilePath;
    //private string _jsonString;
    private JsonTextReader _reader;
    private static ManualLogSource _logger;

    /// <summary>
    /// A json reader class made by Hyperion_21. Construct using a file path to the target .json file.
    /// </summary>
    /// <param name="jsonFilePath"></param>
    public HyperionJsonReader(string jsonFilePath)
    {
        this._jsonFilePath = jsonFilePath;
        //this._jsonString = File.ReadAllText(jsonFilePath);
        _reader = new(new StringReader(File.ReadAllText(jsonFilePath)));
        _logger = HyperionTechTreePlugin.HyperionLog;

        _logger.LogInfo($"jsonString: {File.ReadAllText(jsonFilePath)}");
    }

    /// <summary>
    /// Inputs a key to look for in the .json, and returns that key's value. Return type depends on the key, so explicit casts may be needed!
    /// </summary>
    /// <param name="key"></param>
    /// <returns name="value"></returns>
    public object GetValueFromKey(string key)
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

    public void ResetJsonReader()
    {
        _logger.LogInfo("Attempting to reset json reader!");
        _reader.Close();
        //_reader = null;
        _reader = new(new StringReader(_jsonFilePath));
    } 
}

*/