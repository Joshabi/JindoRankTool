using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliceMapDoublesAnalyser : SliceMapBucketedAnalyser
{

    struct SingleDoubleCounter
    {
        public int SingleCount;
        public int DoubleCount;
    }

    private SingleDoubleCounter[] _singlesDoublesBuckets;

    enum HandMode
    {
        Left,
        Right,
    }

    public SliceMapDoublesAnalyser()
    {
    }

    public override void ProcessSliceMaps(BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        base.ProcessSliceMaps(mapMetadata, leftHand, rightHand);

        float bpm = mapMetadata.bpm;
        int bucketCount = GetBucketCount();
        _singlesDoublesBuckets = new SingleDoubleCounter[bucketCount];

        HandMode currentHand = HandMode.Left;
        int leftSliceIndex = 0;
        int rightSliceIndex = 0;
        int leftSliceCount = leftHand.GetSliceCount();
        int rightSliceCount = rightHand.GetSliceCount();
        BeatCutData leftBeatCutData = leftHand.GetBeatCutData(0);
        BeatCutData rightBeatCutData = rightHand.GetBeatCutData(0);
        while (leftSliceIndex < leftSliceCount && rightSliceIndex < rightSliceCount)
        {
            bool isDouble = false;
            if (Mathf.Abs(leftBeatCutData.sliceStartBeat - rightBeatCutData.sliceStartBeat) <= Mathf.Epsilon)
            {
                if (leftBeatCutData.notesInCut != null && rightBeatCutData.notesInCut != null)
                {
                    isDouble = true;
                }
            }
            else if (leftBeatCutData.sliceStartBeat > rightBeatCutData.sliceStartBeat && currentHand == HandMode.Left)
            {
                currentHand = HandMode.Right;
            }
            else if (rightBeatCutData.sliceStartBeat > leftBeatCutData.sliceStartBeat && currentHand == HandMode.Right)
            {
                currentHand = HandMode.Left;
            }

            int bucketIndex = GetBucketIndexFromBeat(bpm, leftBeatCutData.sliceStartBeat);
            if (isDouble)
            {
                _singlesDoublesBuckets[bucketIndex].DoubleCount++;
            }
            else
            {
                _singlesDoublesBuckets[bucketIndex].SingleCount++;
            }

            if (currentHand == HandMode.Left)
            {
                ++leftSliceIndex;
                if (leftSliceIndex < leftSliceCount)
                {
                    leftBeatCutData = leftHand.GetBeatCutData(leftSliceIndex);
                }
                else
                {
                    currentHand = HandMode.Right;
                }
            }
            else
            {
                ++rightSliceIndex;
                if (rightSliceIndex < rightSliceCount)
                {
                    rightBeatCutData = rightHand.GetBeatCutData(rightSliceIndex);
                }
                else
                {
                    currentHand = HandMode.Left;
                }
            }
        }

        int totalDoubleCuts = 0;
        for (int index = 0; index < bucketCount; ++index)
        {
            int singleCount = _singlesDoublesBuckets[index].SingleCount;
            int doubleCount = _singlesDoublesBuckets[index].DoubleCount;
            if (singleCount == 0 && doubleCount == 0)
            {
                SetBucketValue(index, 0);
            }
            else
            {
                float ratio = doubleCount / (1.0f * (singleCount + doubleCount));
                SetBucketValue(index, ratio);
            }
            totalDoubleCuts += doubleCount;
        }

        int totalCuts = leftSliceCount + rightSliceCount;
        SetOverallValue(totalDoubleCuts / (1.0f * totalCuts));
    }

    public override string GetAnalyticsName()
    {
        return "doubles";
    }

    public override string GetAnalyticsDescription()
    {
        return "Returns a ratio of double swings to single swings. Double swings are counted as one swing in the ratio. 0 = No double swings, 1 = all swings are doubles.";
    }
}
