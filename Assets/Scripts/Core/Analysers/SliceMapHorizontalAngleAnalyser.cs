using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SliceMapPerHandHorizontalAngleRatio
{
    public float leftHand;
    public float rightHand;
}

public class SliceMapHorizontalAngleAnalyser : SliceMapSeparateHandAnalyser
{
    public override string GetAnalyticsDescription()
    {
        return "Returns a 0-1 range factor where 0=all verticals, and 1=all horizontals (diagonals count as 0.5)";
    }

    public override string GetAnalyticsName()
    {
        return "horizontalAngleRatio";
    }

    protected override void ProcessHand(SliceMap hand, bool isLeftHand)
    {
        int bucketCount = GetBucketCount();
        int totalNotes = 0;
        float totalHorizontalRatio = 0.0f;
        int[] notesInBucket = new int[bucketCount];
        float[] bucketRatioBuffer = new float[bucketCount];

        int sliceCount = hand.GetSliceCount();
        for (int sliceIndex = 0; sliceIndex < sliceCount; ++sliceIndex)
        {
            BeatCutData cutData = hand.GetBeatCutData(sliceIndex);

            if (cutData.notesInCut != null)
            {
                foreach (ColourNote note in cutData.notesInCut)
                {
                    int bucketIndex = GetBucketIndexFromBeat(note.b);
                    float ratio = GetHorizontalFactorFromNoteDirection((BeatCutDirection)note.d);
                    ++notesInBucket[bucketIndex];
                    bucketRatioBuffer[bucketIndex] += ratio;
                    totalHorizontalRatio += ratio;
                    ++totalNotes;
                }
            }

            ++sliceIndex;
        }

        for (int bucketIndex = 0; bucketIndex < bucketCount; ++bucketIndex)
        {
            int noteCount = notesInBucket[bucketIndex];
            float ratio = (noteCount > 0) ? bucketRatioBuffer[bucketIndex] / noteCount : 0.0f;
            UpdateBucketValue(bucketIndex, ratio, isLeftHand);
        }

        UpdateOverallValue(totalHorizontalRatio / totalNotes, isLeftHand);
    }

    private float GetHorizontalFactorFromNoteDirection(BeatCutDirection d)
    {
        switch (d)
        {
            case BeatCutDirection.Up: return 0.0f;
            case BeatCutDirection.Down: return 0.0f;
            case BeatCutDirection.Left: return 1.0f;
            case BeatCutDirection.Right: return 1.0f;
            case BeatCutDirection.UpLeft: return 0.5f;
            case BeatCutDirection.UpRight: return 0.5f;
            case BeatCutDirection.DownLeft: return 0.5f;
            case BeatCutDirection.DownRight: return 0.5f;
            case BeatCutDirection.Any: return 0.0f;
        }

        return 0.0f;
    }
}
