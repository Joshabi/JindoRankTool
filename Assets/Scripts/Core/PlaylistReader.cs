using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaylistReader
{

    [System.Serializable]
    public struct JSONBeatSaberPlaylist
    {
        public List<JSONBeatSaberPlaylistSong> songs;
    }

    [System.Serializable]
    public struct JSONBeatSaberPlaylistSong
    {
        public string hash;
        public List<JSONBeatSaberPlaylistSongDifficulty> difficulties;
    }

    [System.Serializable]
    public struct JSONBeatSaberPlaylistSongDifficulty
    {
        public string characteristic;
        public string name;
    }

    public Dictionary<string, HashSet<BeatmapDifficultyRank>> GetMapsFromPlaylist(string playlistJSON)
    {
        Dictionary<string, HashSet<BeatmapDifficultyRank>> returnDictionary = new Dictionary<string, HashSet<BeatmapDifficultyRank>>();
        JSONBeatSaberPlaylist playlist = JsonUtility.FromJson<JSONBeatSaberPlaylist>(playlistJSON);

        foreach (JSONBeatSaberPlaylistSong song in playlist.songs)
        {
            HashSet<BeatmapDifficultyRank> difficulties = new HashSet<BeatmapDifficultyRank>();
            foreach (JSONBeatSaberPlaylistSongDifficulty diff in song.difficulties)
            {
                // TODO: support non-standard characteristics, not required for our first use-case.
                if (!diff.characteristic.ToLower().Equals("standard"))
                {
                    continue;
                }

                difficulties.Add(BeatmapUtils.StringToDifficulty(diff.name));
            }

            if (difficulties.Count > 0)
            {
                returnDictionary.Add(song.hash, difficulties);
            }
        }

        return returnDictionary;
    }

}
