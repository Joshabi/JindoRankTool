using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISliceMapAnalyser
{

    void ProcessSliceMaps(BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand);

    string GetAnalyticsName();

    string GetAnalyticsData();

}
