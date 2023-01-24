using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO.Compression;
using System.IO;

[System.Serializable]
public struct BeatSaverMapDownloadDataVersionedData
{
    public string downloadURL;
}


[System.Serializable]
public struct BeatSaverMapDownloadData
{
    public List<BeatSaverMapDownloadDataVersionedData> versions;
}


public class LevelDownloader : MonoBehaviour
{

    public delegate void LevelDownloaderEvent(string path);
    public delegate void LevelDownloadsCompleteEvent();
    public event LevelDownloaderEvent OnLevelDownloaded;
    public event LevelDownloadsCompleteEvent OnLevelDownloadsCompleted;
    
    private readonly string apiMapIDURL = "https://api.beatsaver.com/maps/id/";

    private int _downloadsRequested = 0;
    private int _downloadsInProgress = 0;
    private int _downloadsComplete = 0;

    public void DownloadLevels(string levelIDCSV)
    {
        if (levelIDCSV.Contains(","))
        {
            string[] levelIDs = levelIDCSV.Split(',');
            foreach (string levelID in levelIDs)
            {
                DownloadLevel(levelID);
            }
        }
        else
        {
            DownloadLevel(levelIDCSV);
        }
    }

    public void DownloadLevel(string levelID)
    {
        ++_downloadsRequested;

        if (levelID.Length == 0 || !ValidateLevelID(levelID))
        {
            ++_downloadsComplete;
            return;
        }

        ++_downloadsInProgress;
        StartCoroutine(GetAPIRequest(levelID));
    }

    private bool ValidateLevelID(string levelID)
    {
        foreach (char character in levelID)
        {
            if ((character >= 'a' && character <= 'f') || (character >= '0' && character <= '9'))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private IEnumerator GetAPIRequest(string levelID)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiMapIDURL + levelID))
        {
            yield return request.SendWebRequest();

            if (request.isDone)
            {
                BeatSaverMapDownloadData downloadData = JsonUtility.FromJson<BeatSaverMapDownloadData>(request.downloadHandler.text);
                if (downloadData.versions.Count > 0)
                {
                    int latestVersion = downloadData.versions.Count - 1;
                    string url = downloadData.versions[latestVersion].downloadURL;
                    yield return GetMapDownloadRequest(url, levelID);
                }
            }
        }
    }

    private IEnumerator GetMapDownloadRequest(string mapURL, string levelID)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(mapURL))
        {
            yield return request.SendWebRequest();

            if (request.isDone)
            {
                using (var compressedFileStream = new MemoryStream())
                {
                    compressedFileStream.Write(request.downloadHandler.data, 0, request.downloadHandler.data.Length);
                    ZipArchive zip = new ZipArchive(compressedFileStream);
                    string mapDirectory = PathUtils.GetImportDirectory() + "/" + levelID + "/";
                    zip.ExtractToDirectory(mapDirectory);
                    --_downloadsInProgress;
                    ++_downloadsComplete;
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

    private void OnApplicationQuit()
    {
        string[] directoryList = System.IO.Directory.GetDirectories(PathUtils.GetImportDirectory());
        foreach (string directory in directoryList)
        {
            string[] files = System.IO.Directory.GetFiles(directory);
            foreach (string file in files)
            {
                System.IO.File.Delete(file);
            }
            System.IO.Directory.Delete(directory);
#if UNITY_EDITOR
            System.IO.File.Delete(directory+".meta");
#endif
        }
    }

}
