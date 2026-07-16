using UnityEngine;

[CreateAssetMenu(fileName = "New Samirin Booth Asset Info", menuName = "Samirin Booth Manager/Samirin Booth Asset Info")]
public class SamirinBoothAssetInfo : ScriptableObject
{
    public string name;
    public string description;
    public Sprite[] images;
    public Category category = Category.AvatarGimmick;
    public int majorVertion;
    public int minorVertion;
    public int patchVertion;
    public DateTime releaseDate;
    public DateTime updateDate;
    public UpdateInfo[] updateInfos;
    public string url;
    public string price;
    public string youtubeUrl;
    public AdditionalInfo[] additionalInfos;
    public Variation[] variations;
    public SamirinBoothAssetInfo[] relatedAssets;
    public string folderName;
}

public enum Category
{
    Avatar,
    AvatarGimmick,
    AvatarAccessory,
    World,
    WorldGimmick,
    _3DModel,
    Other
}

[System.Serializable]
public class Variation
{
    public string variationName;
    public string variationDescription;
    public string prefabPath;
}

[System.Serializable]
public class DateTime
{
    public int year;
    public int month;
    public int day;
}

[System.Serializable]
public class UpdateInfo
{
    public string updateName;
    public string updateDescription;
    public DateTime updateDate;
}

[System.Serializable]
public class AdditionalInfo
{
    public string title;
    public string description;
    public Sprite image;
    public string[] paths;
}