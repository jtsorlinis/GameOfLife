Shader "Unlit/quadShader" {
  SubShader {
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #define lineThickness 0.1
      #define gridFadeZoom 1.5

      uint width;
      uint gridWidth;
      uint height;
      float zoom;
      StructuredBuffer<uint> grid;

      uint getBit(uint input, uint pos) {
        return (input >> (pos)) & 1;
      }

      void vert(inout float4 vertex : POSITION, inout float2 uv : TEXCOORD0) {
        vertex = UnityObjectToClipPos(vertex);
      }

      fixed4 frag(float4 vertex : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target {
        float2 cellPos = uv * float2(width, height);
        float2 pixel = fwidth(cellPos);
        // Common resolutions put some cell edges exactly on pixel centres (e.g. 1080 / 128 rows = 8.4375px per
        // cell), leaving the line test to float rounding. Shifting by an awkward fraction of a pixel (~1/pi)
        // avoids those ties, and cells use the same position so they stay aligned with the lines
        cellPos = max(cellPos - pixel * 0.3183, 0);

        // Draw grid
        float2 thickness = max(1, round(lineThickness / pixel)) * pixel;
        float2 cellFrac = frac(cellPos);
        if (zoom < gridFadeZoom && (cellFrac.x < thickness.x || cellFrac.y < thickness.y)) {
          return (gridFadeZoom - zoom) * 0.2;
        }

        uint xpos = cellPos.x;
        uint ypos = cellPos.y;
        uint gridIndex = ypos * gridWidth + xpos / 32;
        return getBit(grid[gridIndex], xpos % 32);
      }
      ENDCG
    }
  }
}
