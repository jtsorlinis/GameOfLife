using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
  bool useGPU = false;
  [SerializeField]
  int resolution = 128;
  int width, gridWidth;
  ulong totalCells;
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

  ComputeBuffer buffer1, buffer2;
  int[] array1, array2;
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
    slider.maxValue = Mathf.Floor(Mathf.Sqrt((1ul << (useGPU ? 34 : 24)) / cam.aspect));
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
      bufferLength = Mathf.NextPowerOfTwo(Mathf.CeilToInt(totalCells / 32f));
      buffer1 = new ComputeBuffer(bufferLength, 4);
      buffer2 = new ComputeBuffer(bufferLength, 4);
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

    if (useGPU)
    {
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
    else
    {
      int length = Mathf.CeilToInt(totalCells / 32f);
      array1 = new int[length];
      array2 = new int[length];
      buffer1.Dispose();
      buffer2.Dispose();
      buffer1 = new ComputeBuffer(length, 4);
      buffer2 = new ComputeBuffer(length, 4);

      for (int y = 1; y < resolution - 1; y++)
      {
        for (int x = 1; x < gridWidth - 1; x++)
        {
          int index = y * gridWidth + x;
          array1[index] = Random.Range(int.MinValue, int.MaxValue);
          array2[index] = 0;
        }
      }
    }
  }

  void Update()
  {
    quadMaterial.SetFloat("zoom", cam.orthographicSize);
    fpsText.text = "FPS: " + (int)(1 / Time.smoothDeltaTime);
    QualitySettings.vSyncCount = paused ? 1 : 0;

    PanZoom();

    if (paused)
    {
      return;
    }
    CalculateLife();
  }

  void CalculateLife()
  {
    if (useGPU)
    {
      computeShader.SetBuffer(0, "gridIn", swap ? buffer2 : buffer1);
      computeShader.SetBuffer(0, "gridOut", swap ? buffer1 : buffer2);
      computeShader.Dispatch(0, computeGroupsX, computeGroupsY, 1);
      quadMaterial.SetBuffer("grid", swap ? buffer1 : buffer2);
    }
    else
    {
      CPULife.CalculateLife(swap ? array2 : array1, swap ? array1 : array2, gridWidth, resolution);
      buffer1.SetData(swap ? array2 : array1);
      quadMaterial.SetBuffer("grid", buffer1);
    }

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

  public void ToggleGPU(bool active)
  {
    useGPU = active;
    Start();
  }

  public void OnDestroy()
  {
    buffer1.Dispose();
    buffer2.Dispose();
  }
}
