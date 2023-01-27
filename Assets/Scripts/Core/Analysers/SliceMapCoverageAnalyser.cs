using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * SliceMapCoverageAnalyser
 * 
 * Provides a coverage factor for a pair of slice maps, where 0 = no blocks, and 1 = every possible position, direction, colour
 * on the acc grid is occupied.
 */
public class SliceMapCoverageAnalyser : SliceMapBucketedAnalyser
{

    public struct AccGridPosition
    {

        public AccGridPosition(ColourNote n)
        {
            x = n.x;
            y = n.y;
            c = n.c;
            d = n.d;
        }

        public int x;
        public int y;
        public int c;
        public int d;
    }

    class BlockFrequencyAccGrid
    {
        public BlockFrequencyAccGrid()
        {
            FrequencyMap = new Dictionary<AccGridPosition, int>();
        }

        public Dictionary<AccGridPosition, int> FrequencyMap;
    }

    private List<BlockFrequencyAccGrid> _bucketsForCoverageCounting;

    public SliceMapCoverageAnalyser()
    {
        _bucketsForCoverageCounting = new List<BlockFrequencyAccGrid>();
    }

    private float GetCoverageFactor(List<BlockFrequencyAccGrid> inBlockCountsList)
    {
        int uniqueBlockPositionCount = GetUniqueBlockPositionCount(CombineAccGrids(inBlockCountsList));
        int maxUniqueBlockPositionCount = 4 * 3 * 2 * 9;
        return uniqueBlockPositionCount / (1.0f * maxUniqueBlockPositionCount);
    }

    private BlockFrequencyAccGrid CombineAccGrids(List<BlockFrequencyAccGrid> inBlockCountsList)
    {
        BlockFrequencyAccGrid returnGrid = new BlockFrequencyAccGrid();
        foreach (BlockFrequencyAccGrid grid in inBlockCountsList)
        {
            foreach (var pair in grid.FrequencyMap)
            {
                if (returnGrid.FrequencyMap.ContainsKey(pair.Key))
                {
                    returnGrid.FrequencyMap[pair.Key] += pair.Value;
                }
                else
                {
                    returnGrid.FrequencyMap.Add(pair.Key, pair.Value);
                }
            }
        }
        return returnGrid;
    }

    private float GetCoverageFactor(BlockFrequencyAccGrid inBlockCounts)
    {
        int uniqueBlockPositionCount = GetUniqueBlockPositionCount(inBlockCounts);
        int maxUniqueBlockPositionCount = 4 * 3 * 2 * 9;
        return uniqueBlockPositionCount / (1.0f * maxUniqueBlockPositionCount);
    }

    private int GetUniqueBlockPositionCount(BlockFrequencyAccGrid inBlockCounts)
    {
        return inBlockCounts.FrequencyMap.Keys.Count;
    }

    private void CountBlocksInSliceMap(float bpm, SliceMap inSliceMap)
    {
        int numCuts = inSliceMap.GetSliceCount();
        int cutIndex = 0;
        while (cutIndex < numCuts)
        {
            BeatCutData cut = inSliceMap.GetBeatCutData(cutIndex);
            if (cut.notesInCut != null)
            {
                foreach (ColourNote note in cut.notesInCut)
                {
                    int bucketIndex = GetBucketIndexFromBeat(bpm, note.b);
                    AccGridPosition position = new AccGridPosition(note);
                    if (!_bucketsForCoverageCounting[bucketIndex].FrequencyMap.ContainsKey(position))
                    {
                        _bucketsForCoverageCounting[bucketIndex].FrequencyMap.Add(position, 1);
                    }
                    else
                    {
                        _bucketsForCoverageCounting[bucketIndex].FrequencyMap[position]++;
                    }
                }
            }
            ++cutIndex;
        }
    }

    public override void ProcessSliceMaps(BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        base.ProcessSliceMaps(mapMetadata, leftHand, rightHand);

        float bpm = mapMetadata.bpm;
        int bucketCount = GetBucketCount();
        for (int bucketIndex = 0; bucketIndex < bucketCount; ++bucketIndex)
        {
            _bucketsForCoverageCounting.Add(new BlockFrequencyAccGrid());
        }

        CountBlocksInSliceMap(bpm, leftHand);
        CountBlocksInSliceMap(bpm, rightHand);

        for (int bucketIndex = 0; bucketIndex < bucketCount; ++bucketIndex)
        {
            SetBucketValue(bucketIndex, GetCoverageFactor(_bucketsForCoverageCounting[bucketIndex]));
        }
        SetOverallValue(GetCoverageFactor(_bucketsForCoverageCounting));
    }

    public override string GetAnalyticsName()
    {
        return "coverage";
    }

    public override string GetAnalyticsDescription()
    {
        return "Returns a percentage denoting how much of the acc grid is covered by unique configurations of notes. 0 = empty acc grid, 1 = every possible direction and colour occupies every space on the grid. Higher values are indicative of more variety in patterns.";
    }
}
