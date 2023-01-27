using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * ISliceMapAnalyser
 * 
 * Interface for an analyser, which takes a calculated slice path for both hands and extracts
 * some metric from it, to be dumped in a JSON file alongside other analysers.
 */
public interface ISliceMapAnalyser
{

    // Receive the slice maps and process them.
    void ProcessSliceMaps(BeatmapStructure mapMetadata, SliceMap leftHand, SliceMap rightHand);

    // Return the name of this analyser.
    string GetAnalyticsName();

    // Return a brief description of this analyser.
    // Serves no purpose beyond informing readers of the JSON what your data represents.
    string GetAnalyticsDescription();

    // Return a JSONified version of the data you collected in ProcessSliceMaps.
    string GetAnalyticsData();

}
