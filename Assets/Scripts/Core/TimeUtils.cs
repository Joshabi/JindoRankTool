using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeUtils
{

    public static float SecondsToBeats(float bpm, float seconds)
    {
        return seconds * (bpm / 60.0f);
    }

    public static float BeatsToSeconds(float bpm, float beat)
    {
        return (60.0f / bpm) * beat;
    }

}
