using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class LevelAudioLoader : MonoBehaviour
{

    public delegate void LevelAudioLoadEvent(AudioClip audioClip);

    public void LoadSong(string filePath, LevelAudioLoadEvent onLevelAudioLoadedCallback)
    {
        StartCoroutine(GetAudioFile(filePath, onLevelAudioLoadedCallback));
    }

    IEnumerator GetAudioFile(string filePath, LevelAudioLoadEvent onLevelAudioLoadedCallback)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.OGGVORBIS))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
            }
            else
            {
                onLevelAudioLoadedCallback(DownloadHandlerAudioClip.GetContent(www));
            }
        }
    }

}
