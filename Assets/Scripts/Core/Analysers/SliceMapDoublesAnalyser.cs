using System.Collections;
using System.Collections.Generic;
using JoshaParity;
using UnityEngine;

public class SliceMapDoublesAnalyser : SliceMapBucketedAnalyser<float>
{

    struct SingleDoubleCounter
    {
        public int SingleCount;
        public int DoubleCount;
    }

    enum HandMode
    {
        Left,
        Right,
    }

    public SliceMapDoublesAnalyser()
    {
    }

    public override void ProcessSwingData(MapDatabase mapDatabase, BeatmapStructure mapMetadata, List<SwingData> leftHand, List<SwingData> rightHand)
    {
        base.ProcessSwingData(mapDatabase, mapMetadata, leftHand, rightHand);

        int bucketCount = GetBucketCount();
        SingleDoubleCounter[] singlesDoublesBuckets = new SingleDoubleCounter[bucketCount];

        HandMode currentHand = HandMode.Left;
        int leftSliceIndex = 0;
        int rightSliceIndex = 0;
        int leftSliceCount = leftHand.Count;
        int rightSliceCount = rightHand.Count;
        while (leftSliceIndex < leftSliceCount && rightSliceIndex < rightSliceCount)
        {
            bool isDouble = false;
            if (Mathf.Abs(leftHand[leftSliceIndex].swingStartBeat - rightHand[rightSliceIndex].swingStartBeat) <= Mathf.Epsilon)
            {
                if (leftHand[leftSliceIndex].notes.Count != 0 && rightHand[rightSliceIndex].notes.Count != 0)
                {
                    isDouble = true;
                }
            }
            else if (leftHand[leftSliceIndex].swingStartBeat > rightHand[rightSliceIndex].swingStartBeat && currentHand == HandMode.Left)
            {
                currentHand = HandMode.Right;
            }
            else if (rightHand[rightSliceIndex].swingStartBeat > leftHand[leftSliceIndex].swingStartBeat && currentHand == HandMode.Right)
            {
                currentHand = HandMode.Left;
            }

            int bucketIndex = GetBucketIndexFromBeat(leftHand[0].swingStartBeat);
            if (isDouble)
            {
                singlesDoublesBuckets[bucketIndex].DoubleCount++;
                ++leftSliceIndex;
                ++rightSliceIndex;
            }
            else
            {
                singlesDoublesBuckets[bucketIndex].SingleCount++;
                if (currentHand == HandMode.Left)
                {
                    ++leftSliceIndex;
                    if (leftSliceIndex >= leftSliceCount)
                    {
                        currentHand = HandMode.Right;
                    }
                }
                else
                {
                    ++rightSliceIndex;
                    if (rightSliceIndex >= rightSliceCount)
                    {
                        currentHand = HandMode.Left;
                    }
                }
            }
        }

        int totalDoubleCuts = 0;
        for (int index = 0; index < bucketCount; ++index)
        {
            int singleCount = singlesDoublesBuckets[index].SingleCount;
            int doubleCount = singlesDoublesBuckets[index].DoubleCount;
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
