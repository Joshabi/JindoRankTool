using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO.Compression;
using System.IO;
using System.Linq;

[System.Serializable]
public struct JSONBeatSaverMapDownloadDataVersionedData
{
    public string hash;
    public string downloadURL;
}


[System.Serializable]
public struct JSONBeatSaverMapDownloadData
{
    public List<JSONBeatSaverMapDownloadDataVersionedData> versions;
}

public enum MapCodeType
{
    None,
    ID,
    Hash
}

[System.Serializable]
public struct MapDownloadRequest
{
    public MapDownloadRequest(MapCodeType inType, string inCode)
    {
        Type = inType;
        Code = inCode;
    }

    public MapCodeType Type;
    public string Code;
}

public class LevelDownloader : MonoBehaviour
{

    public delegate void LevelDownloaderEvent(string path);
    public delegate void LevelDownloadsCompleteEvent();
    public event LevelDownloaderEvent OnLevelDownloaded;
    public event LevelDownloadsCompleteEvent OnLevelDownloadsCompleted;

    private readonly string apiMapIDURL = "https://api.beatsaver.com/maps/id/";
    private readonly string apiMapHashURL = "https://api.beatsaver.com/maps/hash/";

    private HashSet<string> _downloadingMapIDSet;
    private int _downloadsRequested = 0;
    private int _downloadsInProgress = 0;
    private int _downloadsComplete = 0;

    private readonly int _codesPerRequest = 50;

    private void Awake()
    {
        _downloadingMapIDSet = new HashSet<string>();
    }

    public void DownloadLevels(List<MapDownloadRequest> inRequestList)
    {
        List<MapDownloadRequest> requestsNotDownloaded = new List<MapDownloadRequest>();
        foreach (MapDownloadRequest request in inRequestList)
        {
            string mapFolder = PathUtils.GetImportDirectory() + "/" + request.Code + "/";
            if (!Directory.Exists(mapFolder))
            {
                requestsNotDownloaded.Add(request);
            }
            else if (OnLevelDownloaded != null)
            {
                OnLevelDownloaded(mapFolder);
            }
        }

        string hashCSV = "";
        int requestBatches = requestsNotDownloaded.Count / _codesPerRequest;
        string[] combinedRequests = new string[requestBatches];
        for (int batch = 0; batch < requestBatches; ++batch)
        {
            hashCSV = "";
            for (int indexInBatch = 0; indexInBatch < _codesPerRequest; ++indexInBatch)
            {
                int index = (batch * _codesPerRequest) + indexInBatch;
                hashCSV += requestsNotDownloaded[index].Code;
                if (indexInBatch < _codesPerRequest-1)
                {
                    hashCSV += ",";
                }
            }
            combinedRequests[batch] = hashCSV;
        }
        StartCoroutine(DownloadAllBatches(combinedRequests));
    }

    private IEnumerator DownloadAllBatches(string[] requests)
    {
        foreach (string batch in requests)
        {
            yield return DownloadBatch(batch);
        }
    }

    private IEnumerator DownloadBatch(string batch)
    {
        yield return GetAPIRequest(new MapDownloadRequest(MapCodeType.Hash, batch));
    }

    public void DownloadLevel(MapDownloadRequest inRequest)
    {
        ++_downloadsRequested;

        if (!ValidateRequest(inRequest))
        {
            ++_downloadsComplete;
            return;
        }

        _downloadingMapIDSet.Add(inRequest.Code);
        ++_downloadsInProgress;
        StartCoroutine(GetAPIRequest(inRequest));
    }

    private bool ValidateRequest(MapDownloadRequest inRequest)
    {
        if (inRequest.Type == MapCodeType.None)
        {
            return false;
        }

        if (inRequest.Code.Length == 0)
        {
            return false;
        }

        if (_downloadingMapIDSet.Contains(inRequest.Code))
        {
            return false;
        }

        foreach (char character in inRequest.Code)
        {
            if ((character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F')
                || (character >= '0' && character <= '9'))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private IEnumerator GetAPIRequest(MapDownloadRequest inRequest)
    {
        string URL = (inRequest.Type == MapCodeType.ID) ? apiMapIDURL : apiMapHashURL;
        using (UnityWebRequest request = UnityWebRequest.Get(URL + inRequest.Code))
        {
            yield return request.SendWebRequest();

            if (request.isDone)
            {
                List<string> codes = inRequest.Code.Split(',').ToList<string>();
                if (codes.Count == 1)
                {
                    JSONBeatSaverMapDownloadData downloadData = JsonUtility.FromJson<JSONBeatSaverMapDownloadData>(request.downloadHandler.text);
                    yield return GetURLFromJson(downloadData);
                }
                else
                {
                    List<JSONBeatSaverMapDownloadData> downloadData = ParseMultipleMapsInToSingleMaps(request.downloadHandler.text);
                    foreach (JSONBeatSaverMapDownloadData data in downloadData)
                    {
                        yield return GetURLFromJson(data);
                    }
                }
            }
        }
    }

    /**
     * For multiple maps in a single JSON file, the beat saver API decided to use the hashes as property keys, which makes it
     * impossible to deserialize with JsonUtility, this project is currently dependency-less so to keep it that way
     * for now this hack manually trudges through the JSON to grab the value of each kv pair.
     */
    private List<JSONBeatSaverMapDownloadData> ParseMultipleMapsInToSingleMaps(string inJSON)
    {
        List<JSONBeatSaverMapDownloadData> returnValue = new List<JSONBeatSaverMapDownloadData>();
        int requiredDepth = 2;
        int nestLevel = 0;
        int startIndex = -1;
        for (int index = 0; index < inJSON.Length; ++index)
        {
            char c = inJSON[index];
            if (c == '{')
            {
                ++nestLevel;
                if (nestLevel == requiredDepth)
                {
                    startIndex = index;
                }
            }
            else if (c == '}')
            {
                --nestLevel;
                if (nestLevel < requiredDepth && startIndex != -1)
                {
                    int length = (index - startIndex) + 1;
                    string parsedJSON = inJSON.Substring(startIndex, length);
                    returnValue.Add(JsonUtility.FromJson<JSONBeatSaverMapDownloadData>(parsedJSON));
                    startIndex = -1;
                }
            }
        }
        return returnValue;
    }

    private IEnumerator GetURLFromJson(JSONBeatSaverMapDownloadData inDownloadData)
    {
        int latestVersion = inDownloadData.versions.Count - 1;
        string url = inDownloadData.versions[latestVersion].downloadURL;
        string code = inDownloadData.versions[latestVersion].hash;
        yield return GetMapDownloadRequest(url, code);
    }

    private IEnumerator GetMapDownloadRequest(string mapURL, string code)
    {
        string mapDirectory = PathUtils.GetImportDirectory() + "/" + code + "/";
        if (Directory.Exists(mapDirectory))
        {
            Debug.LogWarning("Directory \"" + code + "\" already existed for the current download. If you want to re-download this map, delete the directory.");

            --_downloadsInProgress;
            ++_downloadsComplete;
            _downloadingMapIDSet.Remove(code);
            if (OnLevelDownloaded != null)
            {
                OnLevelDownloaded(mapDirectory);
            }

            yield return null;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(mapURL))
        {
            yield return request.SendWebRequest();

            if (request.isDone)
            {
                using (var compressedFileStream = new MemoryStream())
                {
                    compressedFileStream.Write(request.downloadHandler.data, 0, request.downloadHandler.data.Length);
                    ZipArchive zip = new ZipArchive(compressedFileStream);
                    try
                    {
                        zip.ExtractToDirectory(mapDirectory);
                    }
                    catch (IOException exception)
                    {
                        Debug.LogWarning("Exception message: " + exception.Message);
                    }
                    --_downloadsInProgress;
                    ++_downloadsComplete;
                    _downloadingMapIDSet.Remove(code);
                    if (OnLevelDownloaded != null)
                    {
                        OnLevelDownloaded(mapDirectory);
                    }
                    if (_downloadsInProgress == 0 && _downloadsComplete == _downloadsRequested && OnLevelDownloadsCompleted != null)
                    {
                        OnLevelDownloadsCompleted();
                    }
                }
            }
        }
    }

}
