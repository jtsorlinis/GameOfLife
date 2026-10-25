using UnityEngine;
using UnityEngine.InputSystem;

public class Main : MonoBehaviour
{
  const int minResolution = 128;
  // Same 0.1 scale the old Input Manager axes applied to mouse movement
  const float pointerSensitivity = 0.1f;

  [SerializeField]
  int resolution = 128;
  int width, gridWidth;
  ulong totalCells;
  [SerializeField]
  bool maxSpeed = false;
  [SerializeField, Range(1, 60)]
  float updateSpeed = 1;

  [Header("Prefabs")]
  [SerializeField] ComputeShader computeShader;
  [SerializeField] GameOfLifeUI ui;
  [SerializeField] Transform quad;
  [SerializeField] Material quadMaterial;

  float minZoom, targetZoom;
  float maxZoom = 0.25f;
  Camera cam;
  bool paused = false;
  bool drawing;
  bool panning;
  int pendingResolution;
  bool pendingReset;

  ComputeBuffer buffer1;
  ComputeBuffer buffer2;
  int bufferLength = 1;
  int computeGroupsX;
  int computeGroupsY;
  float timer = 0;
  bool swap;

  void Awake()
  {
    buffer1 = new ComputeBuffer(bufferLength, 4);
    buffer2 = new ComputeBuffer(bufferLength, 4);

    ui.SetPaused(paused);
    ui.SetTurbo(maxSpeed);
    ui.ResolutionChanged += res => pendingResolution = res;
    ui.PausedChanged += value => paused = value;
    ui.TurboChanged += value => maxSpeed = value;
    ui.RandomiseClicked += () => pendingReset = true;
    ui.ClearClicked += Clear;
  }

  void Start()
  {
    cam = Camera.main;
    var maxResolution = (int)Mathf.Floor(Mathf.Sqrt((1ul << 34) / cam.aspect));
    maxResolution -= maxResolution % 2;
    swap = false;

    // Round to nearest even
    resolution = Mathf.Clamp(resolution, minResolution, maxResolution);
    resolution -= resolution % 2;
    width = WidthFor(resolution);
    gridWidth = width / 32;
    totalCells = (ulong)width * (ulong)resolution;

    if (buffer1.count < Mathf.CeilToInt(totalCells / 32f))
    {
      buffer1.Dispose();
      buffer2.Dispose();
      var newsize = Mathf.NextPowerOfTwo(Mathf.CeilToInt(totalCells / 32f));
      buffer1 = new ComputeBuffer(newsize, 4);
      buffer2 = new ComputeBuffer(newsize, 4);
    }

    cam.orthographicSize = resolution / 128f;
    cam.transform.position = new Vector3(0, 0, -10);
    targetZoom = cam.orthographicSize;
    minZoom = cam.orthographicSize * 1.05f;
    quad.localScale = new Vector2(width / 64f, resolution / 64f);
    ui.SetLimits(minResolution, maxResolution, CellsFor(minResolution), CellsFor(maxResolution));
    ui.SetGrid(resolution, totalCells);

    quadMaterial.SetBuffer("grid", buffer1);
    quadMaterial.SetInteger("height", resolution);
    quadMaterial.SetInteger("width", width);
    quadMaterial.SetInt("gridWidth", gridWidth);

    computeShader.SetInt("rng_state", Random.Range(0, int.MaxValue));
    computeShader.SetInt("width", width);
    computeShader.SetInt("gridWidth", gridWidth);
    computeShader.SetInt("height", resolution);

    computeGroupsX = Mathf.CeilToInt(width / 256f);
    computeGroupsY = Mathf.CeilToInt(resolution / 8f);

    // Clear previous grid
    computeShader.SetBuffer(2, "gridOut", buffer1);
    computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);
    computeShader.SetBuffer(2, "gridOut", buffer2);
    computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);

    // Generate new grid
    computeShader.SetBuffer(1, "gridOut", buffer1);
    computeShader.Dispatch(1, computeGroupsX, computeGroupsY, 1);
  }

  void Update()
  {
    // Restart here rather than from the UI callbacks, which can run after this frame's dispatches
    // and free the buffers they use before the frame renders
    if (pendingResolution > 0 || pendingReset)
    {
      if (pendingResolution > 0)
      {
        resolution = pendingResolution;
      }
      pendingResolution = 0;
      pendingReset = false;
      Start();
    }

    QualitySettings.vSyncCount = (maxSpeed && !paused) ? 0 : 1;

    HandleDrawing();

    PanZoom();

    if (paused)
    {
      return;
    }

    if (maxSpeed)
    {
      CalculateLife();
    }
    else
    {
      timer -= Time.deltaTime;
      if (timer < 0)
      {
        CalculateLife();
        timer = 1 / updateSpeed;
      }
    }
  }

  void HandleDrawing()
  {
    // Draw grid when zoomed in
    quadMaterial.SetFloat("zoom", cam.orthographicSize);

    var pointer = Pointer.current;
    var keyboard = Keyboard.current;
    if (pointer == null)
    {
      return;
    }

    // Draw with the left button (or a touch), unless the press started on the panel
    var pointerPosition = pointer.position.ReadValue();
    if (pointer.press.wasPressedThisFrame)
    {
      drawing = !GameOfLifeUI.IsPointerOverUI(pointerPosition);
    }
    drawing &= pointer.press.isPressed;

    // Holding C erases under the cursor without a click
    var erase = keyboard != null && keyboard.cKey.isPressed && !GameOfLifeUI.IsPointerOverUI(pointerPosition);
    if (drawing || erase)
    {
      var mousePos = cam.ScreenToWorldPoint(pointerPosition);
      computeShader.SetVector("mousePos", mousePos);
      computeShader.SetBool("erase", erase);
      computeShader.SetBuffer(3, "gridOut", swap ? buffer2 : buffer1);
      computeShader.Dispatch(3, 1, 1, 1);
    }
  }

  void Clear()
  {
    paused = true;
    ui.SetPaused(paused);
    computeShader.SetBuffer(2, "gridOut", buffer1);
    computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);
    computeShader.SetBuffer(2, "gridOut", buffer2);
    computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);
  }

  void CalculateLife()
  {
    computeShader.SetBuffer(0, "gridIn", swap ? buffer2 : buffer1);
    computeShader.SetBuffer(0, "gridOut", swap ? buffer1 : buffer2);
    computeShader.Dispatch(0, computeGroupsX, computeGroupsY, 1);
    quadMaterial.SetBuffer("grid", swap ? buffer1 : buffer2);
    swap = !swap;
  }

  // UI related stuff
  void PanZoom()
  {
    var pointer = Pointer.current;
    var mouse = Mouse.current;
    var keyboard = Keyboard.current;
    var pointerPosition = pointer != null ? pointer.position.ReadValue() : Vector2.zero;

    // Don't zoom while scrolling over the panel
    var scroll = mouse != null ? mouse.scroll.ReadValue().y : 0;
    if (scroll != 0 && GameOfLifeUI.IsPointerOverUI(pointerPosition))
    {
      scroll = 0;
    }
    targetZoom -= scroll * (cam.orthographicSize / 10);
    if (targetZoom < maxZoom)
    {
      targetZoom = maxZoom;
    }
    else if (targetZoom > minZoom)
    {
      targetZoom = minZoom;
    }
    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 10);

    // Right-drag pans, unless the press started on the panel
    if (mouse != null && mouse.rightButton.wasPressedThisFrame)
    {
      panning = !GameOfLifeUI.IsPointerOverUI(pointerPosition);
    }
    panning &= mouse != null && mouse.rightButton.isPressed;
    if (panning)
    {
      Cursor.lockState = CursorLockMode.Locked;
      var pan = mouse.delta.ReadValue() * pointerSensitivity;
      cam.transform.position -= new Vector3(pan.x, pan.y) * (cam.orthographicSize / 50);
    }
    else
    {
      Cursor.lockState = CursorLockMode.None;
    }

    if (keyboard == null)
    {
      return;
    }

    // Pause play on space
    if (keyboard.spaceKey.wasPressedThisFrame)
    {
      paused = !paused;
      ui.SetPaused(paused);
    }

    // Quit on escape
    if (keyboard.escapeKey.isPressed)
    {
      Application.Quit();
    }
  }

  // Grid width for a given height, matching the screen's aspect in whole 32 cell words
  int WidthFor(int height)
  {
    var w = (int)(height * cam.aspect);
    return w - w % 32;
  }

  ulong CellsFor(int height)
  {
    return (ulong)WidthFor(height) * (ulong)height;
  }

  public void OnDestroy()
  {
    buffer1.Dispose();
    buffer2.Dispose();
  }
}
