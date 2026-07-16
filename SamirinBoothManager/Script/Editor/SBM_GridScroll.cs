using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SBM_GridScroll
{
    const float SpeedPixelsPerSecond = 16f;
    const float DisplayTileSize = 25f;

    readonly VisualElement _gridLayer;
    readonly float _loopSize;
    IVisualElementScheduledItem _schedule;
    float _offset;
    double _lastUpdateTime;

    public SBM_GridScroll(VisualElement gridRoot)
    {
        _gridLayer = gridRoot.childCount > 0 ? gridRoot[0] : gridRoot;
        _loopSize = DisplayTileSize;
        SetupTiling();
    }

    public static SBM_GridScroll Attach(VisualElement root)
    {
        var grid = root.Q<VisualElement>("Grid");
        if (grid == null)
            return null;

        return new SBM_GridScroll(grid);
    }

    void SetupTiling()
    {
        _gridLayer.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
        _gridLayer.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(_loopSize, _loopSize));
    }

    public void Start()
    {
        _lastUpdateTime = EditorApplication.timeSinceStartup;
        _schedule = _gridLayer.schedule.Execute(Update).Every(0);
    }

    void Update()
    {
        var now = EditorApplication.timeSinceStartup;
        var deltaTime = (float)(now - _lastUpdateTime);
        _lastUpdateTime = now;

        _offset = (_offset + SpeedPixelsPerSecond * deltaTime) % _loopSize;
        var offset = new Length(_offset, LengthUnit.Pixel);
        _gridLayer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, offset);
        _gridLayer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, offset);
    }

    public void Stop()
    {
        _schedule?.Pause();
        _schedule = null;
    }
}
