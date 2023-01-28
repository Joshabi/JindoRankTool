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
public struct JSONBeatSaverMapDownloadMetadata
{
    public float duration;
    public float bpm;
}

[System.Serializable]
public struct JSONBeatSaverMapDownloadData
{
    public JSONBeatSaverMapDownloadMetadata metadata;
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

    public delegate void LevelDownloaderEvent(string path, JSONBeatSaverMapDownloadData downloadData);
    public delegate void LevelDownloadsCompleteEvent();
    public event LevelDownloaderEvent OnLevelDownloaded;
    public event LevelDownloadsCompleteEvent OnLevelDownloadsComplete;

    private readonly string apiMapIDURL = "https://api.beatsaver.com/maps/id/";
    private readonly string apiMapHashURL = "https://api.beatsaver.com/maps/hash/";

    private HashSet<string> _downloadingMapIDSet;
    private int _downloadsRequested = 0;
    private int _downloadsComplete = 0;

    private readonly int _codesPerRequest = 50;

    private void Awake()
    {
        _downloadingMapIDSet = new HashSet<string>();
    }

    public void DownloadLevels(List<MapDownloadRequest> inRequestList)
    {
        List<MapDownloadRequest> requestsNotDownloaded = new List<MapDownloadRequest>();
        List<string> correctlyDownloadedRequests = new List<string>();
        foreach (MapDownloadRequest request in inRequestList)
        {
            Debug.Log("DownloadLevels: " + request.Code);

            bool folderExists = false;
            bool mapHasBeenProperlyDownloaded = false;
            string mapFolder = PathUtils.GetImportDirectory() + "/" + request.Code + "/";
            if (Directory.Exists(mapFolder))
            {
                Debug.Log(mapFolder + " already exists.");
                folderExists = true;
                try
                {
                    string downloadDataJSON = System.IO.File.ReadAllText(mapFolder + "download.json");
                    JSONBeatSaverMapDownloadData downloadData = JsonUtility.FromJson<JSONBeatSaverMapDownloadData>(downloadDataJSON);
                    mapHasBeenProperlyDownloaded = true;
                    Debug.Log(mapFolder + " has been properly downloaded and will be skipped.");
                    if (OnLevelDownloaded != null)
                    {
                        OnLevelDownloaded(mapFolder, downloadData);
                    }
                    continue;
                }
                catch (System.IO.FileNotFoundException e)
                {
                    Debug.LogWarning(e);
                }
            }

            if (!mapHasBeenProperlyDownloaded)
            {
                Debug.Log(request.Code + " was not downloaded properly before, the folder is being cleaned up and the download re-attempted.");
                if (folderExists)
                {
                    // Clean up the folder and redo the download properly
                    string[] paths = System.IO.Directory.GetFiles(mapFolder);
                    foreach (string path in paths)
                    {
                        System.IO.File.Delete(path);
                    }
                    System.IO.Directory.Delete(mapFolder);
                }
                requestsNotDownloaded.Add(request);
            }
            else
            {
                correctlyDownloadedRequests.Add(request.Code);
            }
        }

        AddToDownloadRequestCounter(inRequestList.Count);
        if (AddToCompletedDownloadRequestCounter(inRequestList.Count - requestsNotDownloaded.Count))
        {
            return;
        }

        foreach (MapDownloadRequest request in requestsNotDownloaded)
        {
            Debug.Log("request not yet downloaded: " + request.Code);
        }

        string hashCSV = "";
        int requestBatches = (requestsNotDownloaded.Count / _codesPerRequest);
        if (requestsNotDownloaded.Count % _codesPerRequest > 0)
        {
            ++requestBatches;
        }
        Debug.Log("requestBatches = " + requestBatches + " (" + requestsNotDownloaded.Count + " / " + _codesPerRequest + ")");
        string[] combinedRequests = new string[requestBatches];
        int codesInCurrentBatch = 0;
        for (int batch = 0; batch < requestBatches; ++batch)
        {
            codesInCurrentBatch = (batch == requestBatches - 1 && requestsNotDownloaded.Count != _codesPerRequest) ? (requestsNotDownloaded.Count % _codesPerRequest) : _codesPerRequest;
            Debug.Log("codesInCurrentBatch = " + codesInCurrentBatch + " (" + (batch == requestBatches - 1) + ") (" + (requestsNotDownloaded.Count % _codesPerRequest) + ") (" + _codesPerRequest + ")");
            hashCSV = "";
            for (int indexInBatch = 0; indexInBatch < codesInCurrentBatch; ++indexInBatch)
            {
                int index = (batch * _codesPerRequest) + indexInBatch;
                Debug.Log("(batch * _codesPerRequest) + indexInBatch = (" + batch + " * " + _codesPerRequest + ") + " + indexInBatch + " = " + index + ", code = " + requestsNotDownloaded[index].Code);
                hashCSV += requestsNotDownloaded[index].Code;
                if (indexInBatch < codesInCurrentBatch - 1)
                {
                    hashCSV += ",";
                }
            }
            combinedRequests[batch] = hashCSV;
        }

        StartCoroutine(DownloadAllBatches(combinedRequests));
    }

    private void AddToDownloadRequestCounter(int count)
    {
        _downloadsRequested += count;
        Debug.Log("Requesting " + count + " downloads, current download request count = " + _downloadsRequested);
    }

    private bool AddToCompletedDownloadRequestCounter(int count)
    {
        _downloadsComplete += count;
        Debug.Log("Completing " + count + " downloads, current completed request count = " + _downloadsComplete);
        if (AreDownloadsComplete() && OnLevelDownloadsComplete != null)
        {
            OnLevelDownloadsComplete();
            return true;
        }
        return false;
    }

    private bool AreDownloadsComplete()
    {
        int downloadsInProgress = _downloadsRequested - _downloadsComplete;
        if (downloadsInProgress < 0)
        {
            Debug.LogError(downloadsInProgress + " downloads in progress.");
        }
        if (_downloadsComplete > _downloadsRequested)
        {
            Debug.LogError("_downloadsComplete-_downloadsRequested = " + (_downloadsComplete - _downloadsRequested));
        }
        return downloadsInProgress == 0 && _downloadsComplete == _downloadsRequested && OnLevelDownloadsComplete != null;
    }

    private IEnumerator DownloadAllBatches(string[] requests)
    {
        foreach (string request in requests)
        {
            yield return GetAPIRequest(new MapDownloadRequest(MapCodeType.Hash, request));
        }
    }

    public void DownloadLevel(MapDownloadRequest inRequest)
    {
        Debug.Log("DownloadLevel(" + inRequest.Code + ")");

        string[] codes = inRequest.Code.Split(',');
        int batchLength = codes.Length;
        AddToDownloadRequestCounter(batchLength);

        if (!ValidateRequest(inRequest))
        {
            AddToCompletedDownloadRequestCounter(batchLength);
            return;
        }

        foreach (string code in codes)
        {
            _downloadingMapIDSet.Add(code);
        }

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
        Debug.Log("GetAPIRequest(\"" + inRequest.Code + "\")");

        if (inRequest.Code.Length == 0)
        {
            Debug.LogWarning("Empty code made it in to GetAPIRequest.");
            yield return null;
        }

        string URL = (inRequest.Type == MapCodeType.ID) ? apiMapIDURL : apiMapHashURL;
        using (UnityWebRequest request = UnityWebRequest.Get(URL + inRequest.Code))
        {
            yield return request.SendWebRequest();

            string[] codes = inRequest.Code.Split(',');
            int numCodes = codes.Length;
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Completed request.");
                if (numCodes == 1)
                {
                    JSONBeatSaverMapDownloadData downloadData = JsonUtility.FromJson<JSONBeatSaverMapDownloadData>(request.downloadHandler.text);
                    string mapDirectory = PathUtils.GetImportDirectory() + "/" + inRequest.Code + "/";
                    if (!Directory.Exists(mapDirectory))
                    {
                        Directory.CreateDirectory(mapDirectory);
                    }
                    File.WriteAllText(mapDirectory + "download.json", request.downloadHandler.text);
                    yield return GetURLFromJson(mapDirectory, downloadData);
                }
                else
                {
                    List<JSONBeatSaverMapDownloadData> downloadDataList = ParseMultipleMapsInToSingleMaps(request.downloadHandler.text);
                    Debug.Log("downloadData Count = " + downloadDataList.Count + " (codes length = " + codes.Length + ")");
                    for (int index = 0; index < downloadDataList.Count; ++index)
                    {
                        JSONBeatSaverMapDownloadData downloadData = downloadDataList[index];
                        string mapDirectory = PathUtils.GetImportDirectory() + "/" + codes[index] + "/";
                        if (!Directory.Exists(mapDirectory))
                        {
                            Directory.CreateDirectory(mapDirectory);
                        }
                        File.WriteAllText(mapDirectory + "download.json", JsonUtility.ToJson(downloadData));
                        yield return GetURLFromJson(mapDirectory, downloadData);
                    }
                }
            }
            else if (request.result != UnityWebRequest.Result.InProgress)
            {
                foreach (string code in codes)
                {
                    _downloadingMapIDSet.Remove(code);
                }
                AddToCompletedDownloadRequestCounter(numCodes);
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

    private IEnumerator GetURLFromJson(string mapFolder, JSONBeatSaverMapDownloadData inDownloadData)
    {
        int latestVersion = inDownloadData.versions.Count - 1;
        string url = inDownloadData.versions[latestVersion].downloadURL;
        string code = inDownloadData.versions[latestVersion].hash;
        yield return GetMapDownloadRequest(mapFolder, url, code, inDownloadData);
    }

    private IEnumerator GetMapDownloadRequest(string mapFolder, string mapURL, string code, JSONBeatSaverMapDownloadData inDownloadData)
    {
        Debug.Log("GetMapDownloadRequest(" + mapURL + "," + code + "," + inDownloadData + ")");

        using (UnityWebRequest request = UnityWebRequest.Get(mapURL))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                using (var compressedFileStream = new MemoryStream())
                {
                    compressedFileStream.Write(request.downloadHandler.data, 0, request.downloadHandler.data.Length);
                    ZipArchive zip = new ZipArchive(compressedFileStream);
                    try
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            if (entry.Name.EndsWith(".dat"))
                            {
                                entry.ExtractToFile(mapFolder + "/" + entry.Name);
                            }
                        }
                    }
                    catch (IOException exception)
                    {
                        Debug.LogWarning("Exception message: " + exception.Message);
                    }
                    _downloadingMapIDSet.Remove(code);
                    Debug.Log("downloaded " + _downloadsComplete + " of " + _downloadsRequested + " (" + (_downloadsRequested - _downloadsComplete) + " in progress)");
                    if (OnLevelDownloaded != null)
                    {
                        OnLevelDownloaded(mapFolder, inDownloadData);
                    }
                }
            }
            else if (request.result != UnityWebRequest.Result.InProgress)
            {
                Debug.LogError(request.error);
                _downloadingMapIDSet.Remove(code);
                if (OnLevelDownloaded != null)
                {
                    OnLevelDownloaded(mapFolder, inDownloadData);
                }
            }
        }

        Debug.Log(mapURL + ", " + code);
        AddToCompletedDownloadRequestCounter(1);
    }

}
