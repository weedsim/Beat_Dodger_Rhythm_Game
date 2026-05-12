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
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace FronkonGames.LUTs.Synthwave
{
  ///------------------------------------------------------------------------------------------------------------------
  /// <summary> Render Pass. </summary>
  /// <remarks> Only available for Universal Render Pipeline. </remarks>
  ///------------------------------------------------------------------------------------------------------------------
  public sealed partial class Synthwave
  {
    [DisallowMultipleRendererFeature]
    private sealed class RenderPass : ScriptableRenderPass
    {
      // Internal use only.
      internal Material material { get; set; }

      private SynthwaveVolume volume;

      private static class ShaderIDs
      {
        internal static readonly int Intensity = Shader.PropertyToID("_Intensity");

        internal static readonly int LUTTexture = Shader.PropertyToID("_LUTTex");
        internal static readonly int LUTParams = Shader.PropertyToID("_LUTParams");

        internal static readonly int Exposure = Shader.PropertyToID("_Exposure");
        internal static readonly int Contrast = Shader.PropertyToID("_Contrast");
        internal static readonly int Tint = Shader.PropertyToID("_Tint");
        internal static readonly int Hue = Shader.PropertyToID("_Hue");
        internal static readonly int Saturation = Shader.PropertyToID("_Saturation");
        internal static readonly int Vibrance = Shader.PropertyToID("_Vibrance");
        internal static readonly int WhiteBalance = Shader.PropertyToID("_WhiteBalance");
        internal static readonly int SplitToningShadows = Shader.PropertyToID("_SplitToningShadows");
        internal static readonly int SplitToningHighlights = Shader.PropertyToID("_SplitToningHighlights");
        internal static readonly int ChannelMixerRed = Shader.PropertyToID("_ChannelMixerRed");
        internal static readonly int ChannelMixerGreen = Shader.PropertyToID("_ChannelMixerGreen");
        internal static readonly int ChannelMixerBlue = Shader.PropertyToID("_ChannelMixerBlue");
        internal static readonly int Shadows = Shader.PropertyToID("_Shadows");
        internal static readonly int Midtones = Shader.PropertyToID("_Midtones");
        internal static readonly int Highlights = Shader.PropertyToID("_Highlights");
        internal static readonly int ShadowsStart = Shader.PropertyToID("_ShadowsStart");
        internal static readonly int ShadowsEnd = Shader.PropertyToID("_ShadowsEnd");
        internal static readonly int HighlightsStart = Shader.PropertyToID("_HighlightsStart");
        internal static readonly int HighlightsEnd = Shader.PropertyToID("_HighlightsEnd");
      }

      private static class Keywords
      {
        internal static readonly string TonemapNeutral = "_TONEMAP_NEUTRAL";
        internal static readonly string TonemapACES = "_TONEMAP_ACES";
        internal static readonly string TonemapReinhard = "_TONEMAP_REINHARD";
      }

      /// <summary> Render pass constructor. </summary>
      public RenderPass() : base()
      {
        profilingSampler = new ProfilingSampler(Constants.Asset.AssemblyName);
      }

      /// <summary> Destroy the render pass. </summary>
      ~RenderPass() => material = null;

      private Texture3D GetLutTexture()
      {
        if (volume?.profile.value == null)
          return null;

        return volume.mode.value == Modes.Quality ? volume.profile.value.quality : volume.profile.value.performance;
      }

      private bool UpdateMaterial()
      {
        material.shaderKeywords = null;
        material.SetFloat(ShaderIDs.Intensity, volume.intensity.value);

        Texture3D texture3D = GetLutTexture();
        if (texture3D == null)
        {
          Log.Warning($"Profile '{volume.profile.value.name}' is missing the '{volume.mode.value}' LUT texture");

          return false;
        }

        material.SetVector(ShaderIDs.LUTParams, new Vector2(1.0f / texture3D.width, texture3D.width - 1.0f));
        material.SetTexture(ShaderIDs.LUTTexture, texture3D);

        switch (volume.tonemap.value)
        {
          case Tonemappers.Neutral: material.EnableKeyword(Keywords.TonemapNeutral); break;
          case Tonemappers.ACES: material.EnableKeyword(Keywords.TonemapACES); break;
          case Tonemappers.Reinhard: material.EnableKeyword(Keywords.TonemapReinhard); break;
        }

        material.SetFloat(ShaderIDs.Exposure, Mathf.Pow(2.0f, volume.exposure.value));
        material.SetFloat(ShaderIDs.Contrast, volume.contrast.value * 0.01f + 1.0f);
        material.SetColor(ShaderIDs.Tint, volume.tint.value);
        material.SetFloat(ShaderIDs.Hue, volume.hue.value * (1.0f / 360.0f));
        material.SetFloat(ShaderIDs.Saturation, volume.saturation.value * 0.01f + 1.0f);
        material.SetFloat(ShaderIDs.Vibrance, volume.vibrance.value * 0.01f);

        material.SetVector(ShaderIDs.WhiteBalance, ColorUtils.ColorBalanceToLMSCoeffs(volume.whiteTemperature.value, volume.whiteTint.value));

        Color splitColor = volume.splitToningShadows.value;
        splitColor.a = volume.splitToningBalance.value * 0.01f;
        material.SetColor(ShaderIDs.SplitToningShadows, splitColor);
        material.SetColor(ShaderIDs.SplitToningHighlights, volume.splitToningHighlights.value);

        material.SetVector(ShaderIDs.ChannelMixerRed, volume.channelMixerRed.value);
        material.SetVector(ShaderIDs.ChannelMixerGreen, volume.channelMixerGreen.value);
        material.SetVector(ShaderIDs.ChannelMixerBlue, volume.channelMixerBlue.value);

        material.SetColor(ShaderIDs.Shadows, volume.shadows.value.linear);
        material.SetFloat(ShaderIDs.ShadowsStart, volume.shadowsStart.value);
        material.SetFloat(ShaderIDs.ShadowsEnd, volume.shadowsEnd.value);
        material.SetColor(ShaderIDs.Midtones, volume.midtones.value.linear);
        material.SetColor(ShaderIDs.Highlights, volume.highlights.value.linear);
        material.SetFloat(ShaderIDs.HighlightsStart, volume.highlightsStart.value);
        material.SetFloat(ShaderIDs.HighlightsEnd, volume.highLightsEnd.value);

        return true;
      }

      /// <inheritdoc/>
      public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
      {
        volume = VolumeManager.instance.stack.GetComponent<SynthwaveVolume>();
        if (material == null || volume == null || volume.IsActive() == false)
          return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        if (resourceData.isActiveTargetBackBuffer == true)
          return;

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        if (cameraData.camera.cameraType == CameraType.SceneView && volume.affectSceneView.value == false || cameraData.postProcessEnabled == false)
          return;

        TextureHandle source = resourceData.activeColorTexture;
        TextureHandle destination = renderGraph.CreateTexture(source.GetDescriptor(renderGraph));

        if (UpdateMaterial() == false)
          return;

        RenderGraphUtils.BlitMaterialParameters pass = new(source, destination, material, 0);
        renderGraph.AddBlitPass(pass, $"{Constants.Asset.AssemblyName}.Pass");

        resourceData.cameraColor = destination;
      }
    }
  }
}
