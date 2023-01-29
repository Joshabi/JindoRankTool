using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PerHandData
{
    public float leftHand;
    public float rightHand;
}

public abstract class SliceMapSeparateHandAnalyser : SliceMapBucketedAnalyser<PerHandData>
{
    public override void ProcessSliceMaps(MapDatabase mapDatabase, BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        base.ProcessSliceMaps(mapDatabase, mapMetadata, leftHand, rightHand);

        ProcessHand(leftHand, isLeftHand: true);
        ProcessHand(rightHand, isLeftHand: false);
    }

    protected abstract void ProcessHand(SliceMap hand, bool isLeftHand);

    protected void UpdateBucketValue(int bucketIndex, float value, bool isLeftHand)
    {
        PerHandData handData = GetBucketValue(bucketIndex);
        if (isLeftHand)
        {
            handData.leftHand = value;
        }
        else
        {
            handData.rightHand = value;
        }
        SetBucketValue(bucketIndex, handData);
    }

    protected void UpdateOverallValue(float value, bool isLeftHand)
    {
        PerHandData overallHandData = GetOverallValue();
        if (isLeftHand)
        {
            overallHandData.leftHand = value;
        }
        else
        {
            overallHandData.rightHand = value;
        }
        SetOverallValue(overallHandData);
    }
}
