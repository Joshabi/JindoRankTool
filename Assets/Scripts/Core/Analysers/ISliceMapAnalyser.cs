using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISliceMapAnalyser
{

    void ProcessSliceMaps(SliceMap leftHand, SliceMap rightHand);

    string GetAnalyticsName();

    string GetAnalyticsData();

}
