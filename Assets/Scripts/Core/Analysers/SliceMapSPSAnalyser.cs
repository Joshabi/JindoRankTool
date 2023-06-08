using System.Collections;
using System.Collections.Generic;
using JoshaParity;
using UnityEngine;

public class SliceMapSPSAnalyser : SliceMapBucketedAnalyser<float>
{

    enum HandMode
    {
        Left,
        Right,
    }

    public override void ProcessSwingData(MapDatabase mapDatabase, BeatmapStructure mapMetadata, List<SwingData> leftHand, List<SwingData> rightHand)
    {
        base.ProcessSwingData(mapDatabase, mapMetadata, leftHand, rightHand);

        HandMode currentHand = HandMode.Left;
        int leftSliceIndex = 0;
        int rightSliceIndex = 0;
        int leftSliceCount = leftHand.Count;
        int rightSliceCount = rightHand.Count;
        SwingData leftSwingData = leftHand[0];
        SwingData rightSwingData = rightHand[0];

        int totalSliceCount = 0;
        int bucketCount = GetBucketCount();
        int[] slicesPerBucket = new int[bucketCount];

        while (leftSliceIndex < leftSliceCount && rightSliceIndex < rightSliceCount)
        {
            float beatTime = 0.0f;
            bool isDouble = false;
            if (Mathf.Abs(leftSwingData.swingStartBeat - rightSwingData.swingStartBeat) <= Mathf.Epsilon)
            {
                if (leftSwingData.notes.Count == 0 && rightSwingData.notes.Count == 0)
                {
                    isDouble = true;
                }
            }
            else if (leftSwingData.swingStartBeat > rightSwingData.swingStartBeat && currentHand == HandMode.Left)
            {
                currentHand = HandMode.Right;
            }
            else if (rightSwingData.swingStartBeat > leftSwingData.swingStartBeat && currentHand == HandMode.Right)
            {
                currentHand = HandMode.Left;
            }

            int bucketIndex = GetBucketIndexFromBeat(leftSwingData.swingStartBeat);
            if(isDouble)
            {
                beatTime = leftSwingData.swingStartBeat;
                ++leftSliceIndex;
                ++rightSliceIndex;
            }
            else
            {
                if (currentHand == HandMode.Left)
                {
                    beatTime = leftSwingData.swingStartBeat;
                    ++leftSliceIndex;
                    if (leftSliceIndex >= leftSliceCount)
                    {
                        currentHand = HandMode.Right;
                    }
                }
                else
                {
                    beatTime = leftSwingData.swingStartBeat;
                    ++rightSliceIndex;
                    if (rightSliceIndex >= rightSliceCount)
                    {
                        currentHand = HandMode.Left;
                    }
                }
            }

            if (rightSliceIndex < rightSliceCount)
            {
                rightSwingData = rightHand[rightSliceIndex];
            }
            if (leftSliceIndex < leftSliceCount)
            {
                leftSwingData = leftHand[leftSliceIndex];
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
