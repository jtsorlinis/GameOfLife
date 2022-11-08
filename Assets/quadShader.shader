Shader "Unlit/quadShader" {
  SubShader {
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #define lineThickness 0.1

      uint width;
      uint gridWidth;
      uint height;
      float zoom;
      StructuredBuffer<uint> grid;
      bool bits[32];

      void get_bits(uint input) {
        for (int bit = 0; bit < 32; ++bit) {
          bits[bit] = (input >> bit) & 1;;
        }
      }

      void vert(inout float4 vertex : POSITION, inout float2 uv : TEXCOORD0) {
        vertex = UnityObjectToClipPos(vertex);
      }

      fixed4 frag(float4 vertex : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target {
        // Draw grid
        if (zoom < .5 && (frac(uv.x * width) < lineThickness || frac(uv.y * height) < lineThickness)) {
          return 0.5 - zoom;
        }

        uint xpos = uv.x * width;
        uint gridxpos = uv.x * gridWidth;
        uint ypos = uv.y * height;
        uint index = ypos * width + xpos;
        uint gridIndex = ypos * gridWidth + gridxpos;
        get_bits(grid[gridIndex]);
        return bits[index % 32];
      }
      ENDCG
    }
  }
}
