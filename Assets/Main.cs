using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
  [SerializeField]
  int resolution = 128;
  int width;
  [SerializeField]
  bool maxSpeed = false;
  [SerializeField, Range(1, 60)]
  float updateSpeed = 1;

  [Header("Prefabs")]
  [SerializeField] ComputeShader computeShader;
  [SerializeField] Text fpsText;
  [SerializeField] Text cellText;

  float zoom = 1;
  float targetZoom = 1;
  Vector2 pan = Vector2.zero;
  Vector2 targetPan = Vector2.zero;

  RenderTexture renderTexture;
  int computeGroupsX;
  int computeGroupsY;
  float timer = 0;

  void Start()
  {
    zoom = 1;
    targetZoom = 1;
    pan = Vector2.zero;
    targetPan = Vector2.zero;

    width = (int)(resolution * Camera.main.aspect);
    renderTexture = new RenderTexture(width, resolution, 0);
    renderTexture.enableRandomWrite = true;

    cellText.text = "Cells: " + (width * resolution);

    computeGroupsX = Mathf.CeilToInt(resolution * Camera.main.aspect / 8f);
    computeGroupsY = Mathf.CeilToInt(resolution / 8f);
    computeShader.SetTexture(1, "Result", renderTexture);
    computeShader.SetTexture(0, "Result", renderTexture);

    computeShader.SetInt("rng_state", Random.Range(0, int.MaxValue));
    computeShader.SetInt("width", width);
    computeShader.SetInt("height", resolution);
    computeShader.Dispatch(1, computeGroupsX, computeGroupsY, 1);
  }

  void OnRenderImage(RenderTexture src, RenderTexture dest)
  {
    Graphics.Blit(renderTexture, dest, new Vector2(zoom, zoom), pan);
  }

  void Update()
  {
    fpsText.text = "FPS: " + (int)(1 / Time.smoothDeltaTime);

    PanZoom();

    if (maxSpeed)
    {
      computeShader.Dispatch(0, computeGroupsX, computeGroupsY, 1);
    }
    else
    {
      timer -= Time.deltaTime;
      if (timer < 0)
      {
        computeShader.Dispatch(0, computeGroupsX, computeGroupsY, 1);
        timer = 1 / updateSpeed;
      }
    }
  }

  // UI related stuff
  void PanZoom()
  {
    float zoomDelta = Input.mouseScrollDelta.y * (zoom / 10);
    targetZoom -= zoomDelta;
    zoom = Mathf.Lerp(zoom, targetZoom, Time.deltaTime * 10);
    targetPan -= new Vector2(zoomDelta / -2, zoomDelta / -2);
    pan = Vector2.Lerp(pan, targetPan, Time.deltaTime * 10);

    if (Input.GetMouseButton(1))
    {
      pan -= new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (zoom / 100);
      targetPan = pan;
    }
  }

  public void SliderChange(float val)
  {
    renderTexture.Release();
    resolution = (int)val;
    Start();
  }

  public void RestartButton()
  {
    Start();
  }

  public void TurboSwitch(bool active)
  {
    maxSpeed = active;
  }
}
