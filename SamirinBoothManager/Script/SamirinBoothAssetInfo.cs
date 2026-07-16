using UnityEngine;

[CreateAssetMenu(fileName = "New Samirin Booth Asset Info", menuName = "Samirin Booth Manager/Samirin Booth Asset Info")]
public class SamirinBoothAssetInfo : ScriptableObject
{
    public string name;
    public string description;
    public Sprite[] images;
    public int majorVertion;
    public int minorVertion;
    public int patchVertion;
    public DateTime releaseDate;
    public DateTime updateDate;
    public string url;
    public HowToUse howToUpload;
    public HowToUse howToUse;
    public Variation variations;
    public SamirinBoothAssetInfo[] relatedAssets;
    public string folderName;
}

public class Variation
{
    public string variationName;
    public string variationDescription;
    public GameObject prefab;
}

[System.Serializable]
public class DateTime
{
    public int year;
    public int month;
    public int day;
}

[System.Serializable]
public class HowToUse
{
    public Step[] steps;
}

[System.Serializable]
public class Step
{
    bool numbered = true;
    public string title;
    public string description;
    public Sprite image;
    public string path;
}