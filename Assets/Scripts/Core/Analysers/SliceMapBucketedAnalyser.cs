using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * SliceMapBucketedAnalyser
 * 
 * Base class for an analyser that wants to bucket its data in to segments of X seconds
 */
public abstract class SliceMapBucketedAnalyser : ISliceMapAnalyser
{

    [System.Serializable]
    protected struct Data
    {
        public float overallValue;
        public float[] bucketValues;
        public float secondsPerBucket;
    }

    private Data _data;
    private float _bucketDurationInSeconds = 10.0f;
    private int _numBuckets = 0;

    public SliceMapBucketedAnalyser()
    {
    }

    public SliceMapBucketedAnalyser(float inBucketDurationInSeconds)
    {
        _bucketDurationInSeconds = inBucketDurationInSeconds;
    }

    public virtual void ProcessSliceMaps(BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        _data = new Data();
        _data.secondsPerBucket = _bucketDurationInSeconds;

        List<BeatCutData> leftCuts = new List<BeatCutData>();
        List<BeatCutData> rightCuts = new List<BeatCutData>();
        leftHand.WriteBeatCutDataToList(leftCuts);
        rightHand.WriteBeatCutDataToList(rightCuts);
        float lastBeat = Mathf.Max(leftCuts[leftCuts.Count - 1].sliceEndBeat, rightCuts[rightCuts.Count - 1].sliceEndBeat);
        _numBuckets = GetBucketIndexFromBeat(mapMetadata.bpm, lastBeat)+1;
        _data.bucketValues = new float[_numBuckets];
    }

    public abstract string GetAnalyticsName();

    public string GetAnalyticsData()
    {
        return JsonUtility.ToJson(_data, prettyPrint: true);
    }

    protected float GetBucketDurationInSeconds()
    {
        return _bucketDurationInSeconds;
    }

    protected int GetBucketIndexFromBeat(float bpm, float beat)
    {
        float seconds = TimeUtils.BeatsToSeconds(bpm, beat);
        int index = Mathf.FloorToInt(seconds / GetBucketDurationInSeconds());
        return index;
    }
    
    protected int GetBucketCount()
    {
        return _numBuckets;
    }

    protected void SetOverallValue(float inOverallValue)
    {
        _data.overallValue = inOverallValue;
    }

    protected void SetBucketValue(int bucketIndex, float inBucketValue)
    {
        if (bucketIndex >= 0 && bucketIndex <= _data.bucketValues.Length)
        {
            _data.bucketValues[bucketIndex] = inBucketValue;
        }
    }

}
