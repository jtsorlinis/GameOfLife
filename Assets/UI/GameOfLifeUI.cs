using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

// Settings panel for the simulation
[RequireComponent(typeof(UIDocument))]
public class GameOfLifeUI : MonoBehaviour
{
  const float fpsInterval = 0.25f;
  const int slowFps = 30;

  public event Action<int> ResolutionChanged;
  public event Action<bool> PausedChanged;
  public event Action<bool> TurboChanged;
  public event Action RandomiseClicked;
  public event Action ClearClicked;

  static GameOfLifeUI current;

  VisualElement card, fpsBadge;
  Label fpsLabel, countLabel, minLabel, maxLabel;
  Button playPauseButton, normalButton, turboButton;
  Slider slider;

  // State is kept here so the sim can push it before or after the panel is built.
  // Resolution is the grid height in cells, the width follows the screen's aspect.
  int resolution = 128;
  int minResolution = 128;
  int maxResolution = 128;
  ulong cells;
  ulong minCells;
  ulong maxCells;
  bool paused;
  bool turbo;

  int frames;
  float fpsTimer;

  void OnEnable()
  {
    current = this;

    var root = GetComponent<UIDocument>().rootVisualElement;
    card = root.Q("card");
    fpsBadge = root.Q("fps-badge");
    fpsLabel = root.Q<Label>("fps");
    countLabel = root.Q<Label>("cell-count");
    minLabel = root.Q<Label>("cell-min");
    maxLabel = root.Q<Label>("cell-max");
    playPauseButton = root.Q<Button>("play-pause");
    normalButton = root.Q<Button>("speed-normal");
    turboButton = root.Q<Button>("speed-turbo");
    slider = root.Q<Slider>("size-slider");

    playPauseButton.clicked += () => SelectPaused(!paused);
    root.Q<Button>("randomise").clicked += () => RandomiseClicked?.Invoke();
    root.Q<Button>("clear").clicked += () => ClearClicked?.Invoke();
    normalButton.clicked += () => SelectTurbo(false);
    turboButton.clicked += () => SelectTurbo(true);
    slider.fill = true;
    slider.RegisterValueChangedCallback(evt =>
    {
      // The slider moves smoothly but only a new grid height restarts the sim
      int newResolution = ResolutionFor(evt.newValue);
      if (newResolution != resolution)
      {
        resolution = newResolution;
        ResolutionChanged?.Invoke(resolution);
      }
    });

    Refresh();
  }

  void OnDisable()
  {
    if (current == this)
    {
      current = null;
    }
  }

  void Update()
  {
    frames++;
    fpsTimer += Time.unscaledDeltaTime;
    if (fpsTimer < fpsInterval)
    {
      return;
    }

    int fps = Mathf.RoundToInt(frames / fpsTimer);
    fpsLabel.text = fps.ToString(CultureInfo.InvariantCulture);
    fpsBadge.EnableInClassList("fps--good", fps >= 55);
    fpsBadge.EnableInClassList("fps--ok", fps >= slowFps && fps < 55);
    fpsBadge.EnableInClassList("fps--bad", fps < slowFps);

    frames = 0;
    fpsTimer = 0;
  }

  public void SetGrid(int newResolution, ulong newCells)
  {
    resolution = newResolution;
    cells = newCells;
    Refresh();
  }

  public void SetLimits(int minRes, int maxRes, ulong minCellCount, ulong maxCellCount)
  {
    minResolution = minRes;
    maxResolution = maxRes;
    minCells = minCellCount;
    maxCells = maxCellCount;
    Refresh();
  }

  public void SetPaused(bool value)
  {
    paused = value;
    Refresh();
  }

  public void SetTurbo(bool value)
  {
    turbo = value;
    Refresh();
  }

  // Asks UI Toolkit what is under the pointer right now. Unlike EventSystem.IsPointerOverGameObject
  // this never uses last frame's hover state and works the same for mouse and touch.
  public static bool IsPointerOverUI(Vector2 screenPosition)
  {
    var panel = current != null ? current.card?.panel : null;
    if (panel == null)
    {
      return false;
    }

    var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
    var picked = panel.Pick(panelPosition);
    return picked != null && (picked == current.card || current.card.Contains(picked));
  }

  void SelectPaused(bool value)
  {
    if (value == paused)
    {
      return;
    }
    SetPaused(value);
    PausedChanged?.Invoke(value);
  }

  void SelectTurbo(bool value)
  {
    if (value == turbo)
    {
      return;
    }
    SetTurbo(value);
    TurboChanged?.Invoke(value);
  }

  void Refresh()
  {
    if (slider == null)
    {
      return;
    }

    // Leave the handle alone while it already shows this grid (e.g. mid drag), and order the
    // range and value updates so the slider never clamps (and fires a change) mid-update
    float minExponent = Log2(minResolution);
    float maxExponent = Log2(maxResolution);
    float exponent = Mathf.Clamp(Log2(resolution), minExponent, maxExponent);
    bool syncValue = slider.value > maxExponent || ResolutionFor(slider.value) != resolution;
    if (slider.lowValue != minExponent)
    {
      slider.lowValue = minExponent;
    }
    if (maxExponent >= slider.highValue)
    {
      slider.highValue = maxExponent;
      if (syncValue)
      {
        slider.SetValueWithoutNotify(exponent);
      }
    }
    else
    {
      if (syncValue)
      {
        slider.SetValueWithoutNotify(exponent);
      }
      slider.highValue = maxExponent;
    }

    countLabel.text = cells.ToString("N0", CultureInfo.InvariantCulture);
    minLabel.text = FormatShort(minCells);
    maxLabel.text = FormatShort(maxCells);
    playPauseButton.text = paused ? "Play" : "Pause";
    playPauseButton.EnableInClassList("action--accent", paused);
    normalButton.EnableInClassList("segment--selected", !turbo);
    turboButton.EnableInClassList("segment--selected", turbo);
  }

  // Slider position is log2 of the grid height, so each doubling gets the same travel
  int ResolutionFor(float exponent)
  {
    // Snap the far end to the exact limit, Pow loses precision up there
    if (exponent >= Log2(maxResolution) - 0.0001f)
    {
      return maxResolution;
    }
    // The sim needs an even height
    int even = Mathf.RoundToInt(Mathf.Pow(2, exponent) / 2) * 2;
    return Mathf.Clamp(even, minResolution, maxResolution);
  }

  static float Log2(int value)
  {
    return (float)Math.Log(value, 2);
  }

  static string FormatShort(ulong count)
  {
    if (count >= 1000000000)
    {
      return (count / 1e9).ToString("0.#", CultureInfo.InvariantCulture) + "B";
    }
    if (count >= 1000000)
    {
      return (count / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M";
    }
    if (count >= 1000)
    {
      return (count / 1e3).ToString("0.#", CultureInfo.InvariantCulture) + "K";
    }
    return count.ToString(CultureInfo.InvariantCulture);
  }
}
