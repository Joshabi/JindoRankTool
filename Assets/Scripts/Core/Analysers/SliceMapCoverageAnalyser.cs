using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/**
 * SliceMapCoverageAnalyser
 * 
 * Provides a coverage factor for a pair of slice maps, where 0 = no blocks, and 1 = every possible position, direction, colour
 * on the acc grid is occupied.
 */
public class SliceMapCoverageAnalyser : SliceMapBucketedAnalyser<float>
{

    [DebuggerDisplay("x = {x}, y = {y}, c = {c}, d = {d}")]
    class AccGridPosition
    {

        public AccGridPosition(ColourNote n)
        {
            x = n.x;
            y = n.y;
            c = n.c;
            d = n.d;
        }

        public int x = 0;
        public int y = 0;
        public int c = 0;
        public int d = 0;

        public override bool Equals(object obj)
        {
            AccGridPosition other = (AccGridPosition)obj;
            if (other != null)
            {
                return x == other.x
                    && y == other.y
                    && c == other.c
                    && d == other.d;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return ((c + 1) * 1000) + (d * 100) + (y * 10) + x;
        }
    }

    class BlockFrequencyAccGrid
    {
        public BlockFrequencyAccGrid()
        {
            FrequencyMap = new Dictionary<AccGridPosition, int>();
        }

        public Dictionary<AccGridPosition, int> FrequencyMap;
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

    public override void ProcessSliceMaps(MapDatabase mapDatabase, BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        base.ProcessSliceMaps(mapDatabase, mapMetadata, leftHand, rightHand);

        List<BlockFrequencyAccGrid> bucketsForCoverageCounting = new List<BlockFrequencyAccGrid>();

        int bucketCount = GetBucketCount();
        for (int bucketIndex = 0; bucketIndex < bucketCount; ++bucketIndex)
        {
            bucketsForCoverageCounting.Add(new BlockFrequencyAccGrid());
        }

        CountBlocksInSliceMap(leftHand, bucketsForCoverageCounting);
        CountBlocksInSliceMap(rightHand, bucketsForCoverageCounting);

        for (int bucketIndex = 0; bucketIndex < bucketCount; ++bucketIndex)
        {
            SetBucketValue(bucketIndex, GetCoverageFactor(bucketsForCoverageCounting[bucketIndex]));
        }
        SetOverallValue(GetCoverageFactor(bucketsForCoverageCounting));
    }

    public override string GetAnalyticsName()
    {
        return "coverage";
    }

    public override string GetAnalyticsDescription()
    {
        return "Returns a percentage denoting how much of the acc grid is covered by unique configurations of notes. 0 = empty acc grid, 1 = every possible direction and colour occupies every space on the grid. Higher values are indicative of more variety in patterns.";
    }

    private void CountBlocksInSliceMap(SliceMap inSliceMap, List<BlockFrequencyAccGrid> bucketsForCoverageCounting)
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
                    if (note.c > 1)
                    {
                        continue;
                    }
                    int bucketIndex = GetBucketIndexFromBeat(note.b);
                    AccGridPosition position = new AccGridPosition(note);
                    if (!bucketsForCoverageCounting[bucketIndex].FrequencyMap.ContainsKey(position))
                    {
                        bucketsForCoverageCounting[bucketIndex].FrequencyMap.Add(position, 1);
                    }
                    else
                    {
                        bucketsForCoverageCounting[bucketIndex].FrequencyMap[position]++;
                    }
                }
            }
            ++cutIndex;
        }
    }
}
