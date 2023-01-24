using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathUtils : MonoBehaviour
{

    public static string GetRootDirectory()
    {
        return Application.dataPath;
    }

    public static string GetImportDirectory()
    {
        return GetRootDirectory() + "/Import";
    }

    public static string GetExportDirectory()
    {
        return GetRootDirectory() + "/Export";
    }

}
