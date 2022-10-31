Shader "Unlit/quadShader" {
  SubShader {
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      uint width;
      uint height;
      StructuredBuffer<uint> grid;

      void vert(inout float4 vertex : POSITION, inout float2 uv : TEXCOORD0) {
        vertex = UnityObjectToClipPos(vertex);
      }

      fixed4 frag(float2 uv : TEXCOORD0) : SV_Target {
        uint xpos = uv.x * width;
        uint ypos = uv.y * height;
        uint index = ypos * width + xpos;
        return grid[index];
      }
      ENDCG
    }
  }
}
