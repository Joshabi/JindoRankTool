using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * SliceMapBucketedAnalyser
 * 
 * Base class for an analyser that wants to bucket its data in to segments of X seconds.
 * 
 * The JSON output supplies an overall value, values for each of the buckets,
 * and a 'secondsPerBucket' value to give context to the density of the bucket data.
 * 
 * Use SetOverallValue and SetBucketValue to update the output data.
 * secondsPerBucket is populated in the ProcessSliceMaps base implementation.
 * 
 * TODO: Probably turn this in to a generic class where T controls the type of overallValue and bucketValues.
 */
public abstract class SliceMapBucketedAnalyser<DataType> : ISliceMapAnalyser
{

    [System.Serializable]
    protected struct Data
    {
        public DataType overallValue;
        public DataType[] bucketValues;
        public float secondsPerBucket;
    }

    private Data _data;
    private float _bucketDurationInSeconds = 10.0f;
    private int _numBuckets = 0;
    private float _currentMapBPM = 0.0f;
    private float _currentMapDuration = 0.0f;

    public SliceMapBucketedAnalyser()
    {
    }

    public SliceMapBucketedAnalyser(float inBucketDurationInSeconds)
    {
        _bucketDurationInSeconds = inBucketDurationInSeconds;
    }

    public virtual void ProcessSliceMaps(MapDatabase mapDatabase, BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand)
    {
        _data = new Data();
        _data.secondsPerBucket = _bucketDurationInSeconds;

        _currentMapBPM = mapDatabase.GetMapBPM(mapMetadata.id);
        _currentMapDuration = mapDatabase.GetMapDuration(mapMetadata.id);

        List<BeatCutData> leftCuts = new List<BeatCutData>();
        List<BeatCutData> rightCuts = new List<BeatCutData>();
        leftHand.WriteBeatCutDataToList(leftCuts);
        rightHand.WriteBeatCutDataToList(rightCuts);
        float lastBeat = Mathf.Max(leftCuts[leftCuts.Count - 1].sliceEndBeat, rightCuts[rightCuts.Count - 1].sliceEndBeat);
        _numBuckets = GetBucketIndexFromBeat(lastBeat)+1;
        _data.bucketValues = new DataType[_numBuckets];
    }

    public abstract string GetAnalyticsName();

    public abstract string GetAnalyticsDescription();

    public string GetAnalyticsData()
    {
        return JsonUtility.ToJson(_data, prettyPrint: true);
    }

    protected float GetCurrentMapBPM()
    {
        return _currentMapBPM;
    }

    protected float GetCurrentMapDuration()
    {
        return _currentMapDuration;
    }

    protected float GetBucketDurationInSeconds()
    {
        return _bucketDurationInSeconds;
    }

    protected int GetBucketIndexFromBeat(float beat)
    {
        float seconds = TimeUtils.BeatsToSeconds(_currentMapBPM, beat);
        int index = Mathf.FloorToInt(seconds / GetBucketDurationInSeconds());
        return index;
    }
    
    protected int GetBucketCount()
    {
        return _numBuckets;
    }

    /**
     * Set the overall value of this analyser's output.
     */
    protected void SetOverallValue(DataType inOverallValue)
    {
        _data.overallValue = inOverallValue;
    }

    /**
     * Get the overall value of this analyser's output.
     */
    protected DataType GetOverallValue()
    {
        return _data.overallValue;
    }

    /**
     * Set the value of the bucket at the given index.
     */
    protected void SetBucketValue(int bucketIndex, DataType inBucketValue)
    {
        if (bucketIndex >= 0 && bucketIndex <= _data.bucketValues.Length)
        {
            _data.bucketValues[bucketIndex] = inBucketValue;
        }
    }

    /**
     * Get the value of the bucket at the given index.
     */
    protected DataType GetBucketValue(int bucketIndex)
    {
        if (bucketIndex >= 0 && bucketIndex <= _data.bucketValues.Length)
        {
            return _data.bucketValues[bucketIndex];
        }

        return default(DataType);
    }

}
