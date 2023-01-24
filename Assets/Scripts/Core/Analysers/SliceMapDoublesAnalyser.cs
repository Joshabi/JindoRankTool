using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SliceMapDoubleData
{

    public float beat;
    public float leftAngle;
    public int leftX;
    public int leftY;
    public int leftNoteCount;
    public float rightAngle;
    public int rightX;
    public int rightY;
    public int rightNoteCount;

}

[System.Serializable]
public struct SliceMapDoubleAnalytics
{
    public float overallDoubleRatio;
    public float[] doubleRatioBucketed;
    public float bucketDurationInSeconds;
    public List<SliceMapDoubleData> doublesFound;
}

public class SliceMapDoublesAnalyser : SliceMapBucketedAnalyser
{

    struct SingleDoubleCounter
    {
        public int SingleCount;
        public int DoubleCount;
    }

    private SingleDoubleCounter[] _singlesDoublesBuckets;
    private SliceMapDoubleAnalytics _analytics;

    enum HandMode
    {
        Left,
        Right,
    }

    public SliceMapDoublesAnalyser()
    {
        _analytics = new SliceMapDoubleAnalytics();
        _analytics.doublesFound = new List<SliceMapDoubleData>();
    }

    public override void ProcessSliceMaps(BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        base.ProcessSliceMaps(mapMetadata, leftHand, rightHand);

        float bpm = mapMetadata.bpm;
        _analytics.doublesFound.Clear();
        int bucketCount = GetBucketCount();
        _analytics.doubleRatioBucketed = new float[bucketCount];
        _singlesDoublesBuckets = new SingleDoubleCounter[bucketCount];
        _analytics.overallDoubleRatio = 0.0f;
        _analytics.bucketDurationInSeconds = GetBucketDurationInSeconds();

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
                SliceMapDoubleData newData = new SliceMapDoubleData();
                newData.beat = leftBeatCutData.sliceStartBeat;
                newData.leftAngle = leftBeatCutData.startPositioning.angle;
                newData.leftX = leftBeatCutData.startPositioning.x;
                newData.leftY = leftBeatCutData.startPositioning.y;
                newData.leftNoteCount = leftBeatCutData.notesInCut.Count;
                newData.rightAngle = rightBeatCutData.startPositioning.angle;
                newData.rightX = rightBeatCutData.startPositioning.x;
                newData.rightY = rightBeatCutData.startPositioning.y;
                newData.rightNoteCount = rightBeatCutData.notesInCut.Count;
                _analytics.doublesFound.Add(newData);

                _singlesDoublesBuckets[bucketIndex].DoubleCount+=2;
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

        for (int index = 0; index < bucketCount; ++index)
        {
            int singleCount = _singlesDoublesBuckets[index].SingleCount;
            int doubleCount = _singlesDoublesBuckets[index].DoubleCount;
            if (singleCount == 0 && doubleCount == 0)
            {
                _analytics.doubleRatioBucketed[index] = 0;
            }
            else
            {
                float ratio = doubleCount / (1.0f * (singleCount + doubleCount));
                _analytics.doubleRatioBucketed[index] = ratio;
            }
        }

        int totalCuts = leftSliceCount + rightSliceCount;
        int totalDoubleCuts = _analytics.doublesFound.Count * 2;
        _analytics.overallDoubleRatio = totalDoubleCuts / (1.0f * totalCuts);
    }

    public override string GetAnalyticsName()
    {
        return "doubles";
    }

    public override string GetAnalyticsData()
    {
        return JsonUtility.ToJson(_analytics, prettyPrint: true);
    }
}
