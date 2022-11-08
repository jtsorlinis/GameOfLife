using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
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
  [SerializeField] Text fpsText;
  [SerializeField] Text cellText;
  [SerializeField] Slider slider;
  [SerializeField] Button playPauseButton;
  [SerializeField] Transform quad;
  [SerializeField] Material quadMaterial;

  float minZoom, targetZoom;
  float maxZoom = 0.25f;
  Camera cam;
  bool paused = false;

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
  }

  void Start()
  {
    cam = Camera.main;
    slider.maxValue = Mathf.Floor(Mathf.Sqrt((1ul << 34) / cam.aspect));
    swap = false;

    // Round to nearest even
    resolution -= resolution % 2;
    width = (int)(resolution * cam.aspect);
    width -= width % 32;
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
    cellText.text = "Cells: " + string.Format("{0:n0}", totalCells);

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
    fpsText.text = "FPS: " + (int)(1 / Time.smoothDeltaTime);
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

    // Clear
    if (Input.GetKeyDown(KeyCode.Backspace))
    {
      paused = true;
      computeShader.SetBuffer(2, "gridOut", buffer1);
      computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);
      computeShader.SetBuffer(2, "gridOut", buffer2);
      computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);
    }

    // Draw
    var erase = Input.GetKey(KeyCode.C);
    var leftMouse = Input.GetMouseButton(0);
    if (leftMouse || erase)
    {
      var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
      var yPixel = (int)((mousePos.y * 64) + (resolution / 2));
      var gridxPixel = (int)((mousePos.x * 2) + (width / 64f));
      var xPixel = (int)((mousePos.x * 64) + (width / 2));
      int mouseIndex = yPixel * width + xPixel;
      int gridIndex = yPixel * gridWidth + gridxPixel;
      computeShader.SetInt("bitPos", mouseIndex % 32);
      computeShader.SetInt("gridIndex", gridIndex);
      computeShader.SetBool("erase", erase);
      computeShader.SetBuffer(3, "gridOut", swap ? buffer2 : buffer1);
      computeShader.Dispatch(3, 1, 1, 1);
    }
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
    targetZoom -= Input.mouseScrollDelta.y * (cam.orthographicSize / 10);
    if (targetZoom < maxZoom)
    {
      targetZoom = maxZoom;
    }
    else if (targetZoom > minZoom)
    {
      targetZoom = minZoom;
    }
    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 10);

    if (Input.GetMouseButton(1))
    {
      Cursor.lockState = CursorLockMode.Locked;
      cam.transform.position -= new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (cam.orthographicSize / 50);
    }
    else
    {
      Cursor.lockState = CursorLockMode.None;
    }

    // Pause play on space
    if (Input.GetKeyDown(KeyCode.Space))
    {
      paused = !paused;
    }
    playPauseButton.image.color = paused ? Color.red : Color.white;

    // Quit on escape
    if (Input.GetKey("escape"))
    {
      Application.Quit();
    }
  }

  public void SliderChange(float val)
  {
    resolution = (int)val;
    Start();
  }

  public void RestartButton()
  {
    Start();
  }

  public void PlayPauseButton()
  {
    paused = !paused;
  }

  public void TurboSwitch(bool active)
  {
    maxSpeed = active;
  }

  public void OnDestroy()
  {
    buffer1.Dispose();
    buffer2.Dispose();
  }
}
