Shader "Unlit/quadShader" {
  SubShader {
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #define lineThickness 0.1

      uint width;
      uint height;
      float zoom;
      StructuredBuffer<uint> grid;

      void vert(inout float4 vertex : POSITION, inout float2 uv : TEXCOORD0) {
        vertex = UnityObjectToClipPos(vertex);
      }

      fixed4 frag(float4 vertex : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target {
        // Draw grid
        if (zoom < .5 && (frac(uv.x * width) < lineThickness || frac(uv.y * height) < lineThickness)) {
          return 0.5 - zoom;
        }

        uint xpos = uv.x * width;
        uint ypos = uv.y * height;
        uint index = ypos * width + xpos;
        return grid[index];
      }
      ENDCG
    }
  }
}
