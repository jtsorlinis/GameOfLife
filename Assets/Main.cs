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
  [SerializeField] Transform quad;
  [SerializeField] Material quadMaterial;

  float minZoom, targetZoom;
  float maxZoom = 0.5f;
  Camera cam;

  ComputeBuffer buffer1;
  ComputeBuffer buffer2;
  int computeGroupsX;
  int computeGroupsY;
  float timer = 0;
  bool swap;

  void Start()
  {
    cam = Camera.main;
    QualitySettings.vSyncCount = maxSpeed ? 0 : 1;
    swap = false;

    width = (int)(resolution * cam.aspect);
    totalCells = width * resolution;
    cam.orthographicSize = resolution / 128f;
    targetZoom = cam.orthographicSize;
    minZoom = cam.orthographicSize * 1.05f;
    quad.localScale = new Vector2(cam.orthographicSize * cam.aspect * 2, cam.orthographicSize * 2);
    cellText.text = "Cells: " + totalCells;

    buffer1 = new ComputeBuffer(totalCells, 4);
    buffer2 = new ComputeBuffer(totalCells, 4);

    quadMaterial.SetBuffer("grid", buffer1);
    quadMaterial.SetInteger("height", resolution);
    quadMaterial.SetInteger("width", width);

    computeShader.SetBuffer(1, "Result", buffer1);
    computeShader.SetInt("rng_state", Random.Range(0, int.MaxValue));
    computeShader.SetInt("width", width);
    computeShader.SetInt("height", resolution);

    computeGroupsX = Mathf.CeilToInt(width / 8f);
    computeGroupsY = Mathf.CeilToInt(resolution / 8f);
    computeShader.Dispatch(1, computeGroupsX, computeGroupsY, 1);
  }

  void Update()
  {
    fpsText.text = "FPS: " + (int)(1 / Time.smoothDeltaTime);

    PanZoom();

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

  void CalculateLife()
  {
    computeShader.SetBuffer(0, "Input", swap ? buffer2 : buffer1);
    computeShader.SetBuffer(0, "Result", swap ? buffer1 : buffer2);
    computeShader.Dispatch(0, computeGroupsX, computeGroupsY, 1);
    quadMaterial.SetBuffer("grid", swap ? buffer1 : buffer2);
    swap = !swap;
  }

  // UI related stuff
  void PanZoom()
  {
    targetZoom -= Input.mouseScrollDelta.y * (cam.orthographicSize / 5);
    if (targetZoom < maxZoom)
    {
      targetZoom = maxZoom;
    }
    else if (targetZoom > minZoom)
    {
      targetZoom = minZoom;
    }
    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 5);

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
    buffer1.Dispose();
    buffer2.Dispose();
    resolution = (int)val;
    Start();
  }

  public void RestartButton()
  {
    buffer1.Dispose();
    buffer2.Dispose();
    Start();
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
