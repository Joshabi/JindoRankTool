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
    public float doubleRatio;
    public List<SliceMapDoubleData> doublesFound;
}

public class SliceMapDoublesAnalyser : ISliceMapAnalyser
{

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

    public void ProcessSliceMaps(SliceMap leftHand, SliceMap rightHand)
    {
        _analytics.doublesFound.Clear();
        _analytics.doubleRatio = 0.0f;

        HandMode currentHand = HandMode.Left;
        int leftSliceIndex = 0;
        int rightSliceIndex = 0;
        int leftSliceCount = leftHand.GetSliceCount();
        int rightSliceCount = rightHand.GetSliceCount();
        BeatCutData leftBeatCutData = leftHand.GetBeatCutData(0);
        BeatCutData rightBeatCutData = rightHand.GetBeatCutData(0);
        while (leftSliceIndex < leftSliceCount && rightSliceIndex < rightSliceCount)
        {
            if (Mathf.Abs(leftBeatCutData.sliceStartBeat - rightBeatCutData.sliceStartBeat) <= Mathf.Epsilon)
            {
                if (leftBeatCutData.notesInCut != null && rightBeatCutData.notesInCut != null)
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

        int totalCuts = leftSliceCount + rightSliceCount;
        int totalDoubleCuts = _analytics.doublesFound.Count * 2;
        _analytics.doubleRatio = totalDoubleCuts / (1.0f * totalCuts);
    }

    public string GetAnalyticsName()
    {
        return "doubles";
    }

    public string GetAnalyticsData()
    {
        return JsonUtility.ToJson(_analytics, prettyPrint: true);
    }
}
