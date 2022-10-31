using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
  [SerializeField]
  int resolution = 128;
  int width, totalCells;
  [SerializeField]
  bool maxSpeed = false;
  [SerializeField, Range(1, 60)]
  float updateSpeed = 1;

  [Header("Prefabs")]
  [SerializeField] ComputeShader computeShader;
  [SerializeField] Text fpsText;
  [SerializeField] Text cellText;
  [SerializeField] Slider slider;
  [SerializeField] Transform quad;
  [SerializeField] Material quadMaterial;

  float minZoom, targetZoom;
  float maxZoom = 0.5f;
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
    slider.maxValue = Mathf.Floor(Mathf.Sqrt((1 << 29) / cam.aspect));
    QualitySettings.vSyncCount = maxSpeed ? 0 : 1;
    swap = false;

    width = (int)(resolution * cam.aspect);
    totalCells = width * resolution;

    // Resize buffer if needed
    if (totalCells > bufferLength)
    {
      buffer1.Dispose();
      buffer2.Dispose();
      bufferLength = Mathf.NextPowerOfTwo(totalCells);
      buffer1 = new ComputeBuffer(bufferLength, 4);
      buffer2 = new ComputeBuffer(bufferLength, 4);
    }

    cam.orthographicSize = resolution / 128f;
    cam.transform.position = new Vector3(0, 0, -10);
    targetZoom = cam.orthographicSize;
    minZoom = cam.orthographicSize * 1.05f;
    quad.localScale = new Vector2(cam.orthographicSize * cam.aspect * 2, cam.orthographicSize * 2);
    cellText.text = "Cells: " + totalCells;

    quadMaterial.SetBuffer("grid", buffer1);
    quadMaterial.SetInteger("height", resolution);
    quadMaterial.SetInteger("width", width);

    computeShader.SetInt("rng_state", Random.Range(0, int.MaxValue));
    computeShader.SetInt("width", width);
    computeShader.SetInt("height", resolution);

    computeGroupsX = Mathf.CeilToInt(width / 8f);
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
    // Clear
    if (Input.GetKeyDown(KeyCode.Backspace))
    {
      computeShader.SetBuffer(2, "gridOut", buffer1);
      computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);
      computeShader.SetBuffer(2, "gridOut", buffer2);
      computeShader.Dispatch(2, computeGroupsX, computeGroupsY, 1);
    }

    // Draw
    if (Input.GetMouseButton(0))
    {
      var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
      var yPixel = (int)((mousePos.y * 64) + (resolution / 2));
      var xPixel = (int)((mousePos.x * 64) + (width / 2));
      int index = yPixel * width + xPixel;
      computeShader.SetInt("mouseIndex", index);
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
    QualitySettings.vSyncCount = maxSpeed ? 0 : 1;
  }

  public void OnDestroy()
  {
    buffer1.Dispose();
    buffer2.Dispose();
  }
}
