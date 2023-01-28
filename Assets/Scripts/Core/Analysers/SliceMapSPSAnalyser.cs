using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliceMapSPSAnalyser : SliceMapBucketedAnalyser
{

    enum HandMode
    {
        Left,
        Right,
    }

    public override void ProcessSliceMaps(MapDatabase mapDatabase, BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        base.ProcessSliceMaps(mapDatabase, mapMetadata, leftHand, rightHand);

        HandMode currentHand = HandMode.Left;
        int leftSliceIndex = 0;
        int rightSliceIndex = 0;
        int leftSliceCount = leftHand.GetSliceCount();
        int rightSliceCount = rightHand.GetSliceCount();
        BeatCutData leftBeatCutData = leftHand.GetBeatCutData(0);
        BeatCutData rightBeatCutData = rightHand.GetBeatCutData(0);
        int totalSliceCount = 0;
        int bucketCount = GetBucketCount();
        int[] slicesPerBucket = new int[bucketCount];

        while (leftSliceIndex < leftSliceCount && rightSliceIndex < rightSliceCount)
        {
            float beatTime = 0.0f;
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

            int bucketIndex = GetBucketIndexFromBeat(leftBeatCutData.sliceStartBeat);
            if(isDouble)
            {
                beatTime = leftBeatCutData.sliceStartBeat;
                ++leftSliceIndex;
                ++rightSliceIndex;
            }
            else
            {
                if (currentHand == HandMode.Left)
                {
                    beatTime = leftBeatCutData.sliceStartBeat;
                    ++leftSliceIndex;
                    if (leftSliceIndex >= leftSliceCount)
                    {
                        currentHand = HandMode.Right;
                    }
                }
                else
                {
                    beatTime = rightBeatCutData.sliceStartBeat;
                    ++rightSliceIndex;
                    if (rightSliceIndex >= rightSliceCount)
                    {
                        currentHand = HandMode.Left;
                    }
                }
            }

            if (rightSliceIndex < rightSliceCount)
            {
                rightBeatCutData = rightHand.GetBeatCutData(rightSliceIndex);
            }
            if (leftSliceIndex < leftSliceCount)
            {
                leftBeatCutData = leftHand.GetBeatCutData(leftSliceIndex);
            }
            ++totalSliceCount;
            ++slicesPerBucket[GetBucketIndexFromBeat(beatTime)];
        }

        float totalSPS = totalSliceCount / GetCurrentMapDuration();
        SetOverallValue(totalSPS);
        for (int bucketIndex = 0; bucketIndex < bucketCount; ++bucketIndex)
        {
            SetBucketValue(bucketIndex, slicesPerBucket[bucketIndex] / GetBucketDurationInSeconds());
        }
    }

    public override string GetAnalyticsName()
    {
        return "sps";
    }

    public override string GetAnalyticsDescription()
    {
        return "The slices-per-second of the map, as well as the SPS of bucketed intervals.";
    }

}
