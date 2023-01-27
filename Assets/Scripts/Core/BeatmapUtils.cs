using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatmapUtils
{
    public static BeatmapDifficultyRank StringToDifficulty(string inDifficulty)
    {
        switch (inDifficulty.ToLower())
        {
            case "easy": return BeatmapDifficultyRank.Easy;
            case "normal": return BeatmapDifficultyRank.Normal;
            case "hard": return BeatmapDifficultyRank.Hard;
            case "expert": return BeatmapDifficultyRank.Expert;
            case "expertplus": return BeatmapDifficultyRank.ExpertPlus;
            case "expert+": return BeatmapDifficultyRank.ExpertPlus;
        }

        return BeatmapDifficultyRank.Easy;
    }

}
