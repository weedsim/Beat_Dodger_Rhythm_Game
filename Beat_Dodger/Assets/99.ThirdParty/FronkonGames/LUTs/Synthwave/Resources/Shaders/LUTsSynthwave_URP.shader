////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Martin Bustos @FronkonGames <fronkongames@gmail.com>. All rights reserved.
//
// THIS FILE CAN NOT BE HOSTED IN PUBLIC REPOSITORIES.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
Shader "Hidden/Fronkon Games/LUTs/Synthwave URP"
{
  Properties
  {
    _MainTex("Main Texture", 2D) = "white" {}
  }

  SubShader
  {
    Tags
    {
      "RenderType" = "Opaque"
      "RenderPipeline" = "UniversalPipeline"
    }
    LOD 100
    ZTest Always ZWrite Off Cull Off

    Pass
    {
      Name "Fronkon Games LUTs Synthwave Pass"

      HLSLPROGRAM
      #pragma vertex LUTsVert
      #pragma fragment LUTsFrag
      #pragma fragmentoption ARB_precision_hint_fastest
      #pragma exclude_renderers d3d9 d3d11_9x ps3 flash
      #pragma multi_compile _ _USE_DRAW_PROCEDURAL

      #include "LUTs.hlsl"
      #pragma multi_compile _ _TONEMAP_ACES _TONEMAP_NEUTRAL _TONEMAP_REINHARD
      #include_with_pragmas "ColorGrading.hlsl"

      TEXTURE3D(_LUTTex);
      float2 _LUTParams;

      half4 LUTsFrag(LUTsVaryings input) : SV_Target
      {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        const float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord).xy;
        const half4 color = SAMPLE_MAIN(uv);
        half4 pixel = color;
        float3 pixelLinear = color.rgb;

#if UNITY_COLORSPACE_GAMMA
        pixelLinear = SRGBToLinear(pixelLinear);
#endif

        pixelLinear = ColorGrading(pixelLinear);
        pixel.rgb = LinearToSRGB(pixelLinear);

        float3 xyz = pixel.rgb * _LUTParams.y * _LUTParams.x + _LUTParams.x * 0.5;
        pixel.rgb = SAMPLE_TEXTURE3D(_LUTTex, sampler_LinearClamp, xyz).rgb;

#if !UNITY_COLORSPACE_GAMMA
        pixel = SRGBToLinear(pixel);
#endif

#if 0
        pixel.rgb = PixelDemo(color.rgb, pixel.rgb, uv);
#endif
        return lerp(color, pixel, _Intensity);
      }
      ENDHLSL
    }
  }
  
  FallBack "Diffuse"
}
