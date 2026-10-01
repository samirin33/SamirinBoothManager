using System;
using UnityEngine;

[CreateAssetMenu(fileName = "New Samirin Booth Asset Info", menuName = "Samirin Booth Manager/Samirin Booth Asset Info")]
public class SamirinBoothAssetInfo : ScriptableObject
{
    /// <summary>Booth 上の表示名。Unity のアセット名（Object.name）とは別。</summary>
    public new string name;
    public string description;
    public Sprite[] images;
    public Category category = Category.AvatarGimmick;
    public int majorVertion;
    public int minorVertion;
    public int patchVertion;
    public SamirinBoothDate releaseDate;
    public SamirinBoothDate updateDate;
    /// <summary>
    /// 推奨バージョン。インポート済みバージョンがこれより低いときに更新通知する。
    /// 0.0.0 のときは通知しない。
    /// </summary>
    public SamirinBoothVersion updateRemind;
    public UpdateInfo[] updateInfos;
    public string url;
    public string price;
    public string salePrice;
    public bool newItemLabel = false;
    public string youtubeUrl;
    public PlatformInfo platformInfo;
    public AdditionalInfo[] additionalInfos;
    public AdditionalInfo[] howToSetupInfos;
    public Variation[] variations;
    public SamirinBoothAssetInfo[] relatedAssets;
    public string folderName;
    public SamirinBoothAssetInfo rootAsset;
    public bool visible = true;

    /// <summary>
    /// インポート済みバージョンが推奨バージョンより低いとき true。
    /// 推奨バージョンが 0.0.0 のときは通知しない。
    /// バージョン不明は推奨を満たしているか判断できないため、推奨バージョンが設定されていれば通知する。
    /// </summary>
    public bool ShouldShowUpdateRemind(Version installed)
    {
        if (!updateRemind.IsSet)
            return false;

        if (installed == null)
            return true;

        return installed < updateRemind.ToVersion();
    }
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
public class PlatformInfo
{
    public bool forPCVR = true;
    public bool forPCDesktop = true;
    public bool forQuest = true;
    public bool forAndroid_iOS = true;
}

[System.Serializable]
public class Variation
{
    public string variationName;
    public string variationDescription;
    public string prefabPath;
    public int id;
}

[System.Serializable]
public struct SamirinBoothVersion
{
    public int major;
    public int minor;
    public int patch;

    public bool IsSet => major > 0 || minor > 0 || patch > 0;

    public Version ToVersion()
    {
        return new Version(Math.Max(0, major), Math.Max(0, minor), Math.Max(0, patch));
    }
}

[System.Serializable]
public class SamirinBoothDate
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
    public SamirinBoothDate updateDate;
}

[System.Serializable]
public class AdditionalPathInfo
{
    static readonly Color DefaultButtonColor = new Color(0x45 / 255f, 0x4c / 255f, 0x4f / 255f, 1f);

    /// <summary>フォーカスボタンなどに表示するタイトル。空なら path を表示。</summary>
    public string title;
    /// <summary>
    /// アセットパス（Assets/... / Packages/...）または、
    /// アバター上にセットされたプレハブ配下の相対ヒエラルキーパス。
    /// </summary>
    public string path;
    /// <summary>フォーカスボタンの背景色。未設定（透明）のときはデフォルト色。</summary>
    public Color buttonColor = new Color(0x45 / 255f, 0x4c / 255f, 0x4f / 255f, 1f);

    public bool IsAssetPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var normalized = path.Replace('\\', '/');
            return normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    public string DisplayTitle =>
        !string.IsNullOrWhiteSpace(title) ? title : (path ?? string.Empty);

    public Color ResolvedButtonColor =>
        buttonColor.a > 0.001f ? buttonColor : DefaultButtonColor;
}

[System.Serializable]
public class AdditionalInfo
{
    public string title;
    public string description;
    public Sprite image;
    public AdditionalPathInfo[] paths;
}