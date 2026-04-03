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
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FronkonGames.LUTs.Cyberpunk
{
  /// <summary> Cyberpunk Volume. </summary>
  [Serializable, VolumeComponentMenu("Fronkon Games/LUTs/Cyberpunk"), HelpURL(Constants.Support.Documentation)]
  public sealed class CyberpunkVolume : VolumeComponent, IPostProcessComponent
  {
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Common settings.

    /// <summary> Controls the intensity of the effect [0, 1]. Default 1. </summary>
    /// <remarks> An effect with Intensity equal to 0 will not be executed. </remarks>
    [FloatSliderWithReset(1.0f, 0.0f, 1.0f)]
    public FloatSliderParameterLinear intensity = new(1.0f, 0.0f, 1.0f);

    #endregion
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Cyberpunk settings.

    /// <summary> Quality or performance mode. </summary>
    public EnumParameterNoInterpolation<Cyberpunk.Modes> mode = new(Cyberpunk.Modes.Quality);

    /// <summary> LUT profile. </summary>
    public ProfileParameter profile = new(null);

    #endregion
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Color grading.

    /// <summary> Tonemap operator. Default None. </summary>
    public EnumParameterNoInterpolation<Cyberpunk.Tonemappers> tonemap = new(Cyberpunk.Tonemappers.None);

    /// <summary> Post exposure. Default 0. </summary>
    [FloatSliderWithReset(0.0f, 0.0f, 10.0f)]
    public FloatSliderParameterNoInterpolation exposure = new(0.0f, 0.0f, 10.0f);

    /// <summary> Contrast [-100, 100]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, -100.0f, 100.0f)]
    public FloatSliderParameterNoInterpolation contrast = new(0.0f, -100.0f, 100.0f);

    /// <summary> Color filter. </summary>
    public ColorParameterNoInterpolation tint = new(Color.white);

    /// <summary> The color wheel [-180, 180]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, -180.0f, 180.0f)]
    public FloatSliderParameterNoInterpolation hue = new(0.0f, -180.0f, 180.0f);

    /// <summary> Intensity of a colors [-100, 100]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, -100.0f, 100.0f)]
    public FloatSliderParameterNoInterpolation saturation = new(0.0f, -100.0f, 100.0f);

    /// <summary> Selective saturation boost [-100, 100]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, -100.0f, 100.0f)]
    public FloatSliderParameterNoInterpolation vibrance = new(0.0f, -100.0f, 100.0f);

    /// <summary> Adjust the perceived temperature, making the image cooler or warmer [-100, 100]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, -100.0f, 100.0f)]
    public FloatSliderParameterNoInterpolation whiteTemperature = new(0.0f, -100.0f, 100.0f);

    /// <summary> Temperature-shifted color [-100, 100]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, -100.0f, 100.0f)]
    public FloatSliderParameterNoInterpolation whiteTint = new(0.0f, -100.0f, 100.0f);

    /// <summary> Tint shadows. </summary>
    public ColorParameterNoInterpolation splitToningShadows = new(Color.gray);

    /// <summary> Tint highlights. </summary>
    public ColorParameterNoInterpolation splitToningHighlights = new(Color.gray);

    /// <summary> Split toning balance [-100, 100]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, -100.0f, 100.0f)]
    public FloatSliderParameterNoInterpolation splitToningBalance = new(0.0f, -100.0f, 100.0f);

    /// <summary> Swap color for red channel. </summary>
    public Vector3ParameterNoInterpolation channelMixerRed = new(Vector3.right);

    /// <summary> Swap color for green channel. </summary>
    public Vector3ParameterNoInterpolation channelMixerGreen = new(Vector3.up);

    /// <summary> Swap color for blue channel. </summary>
    public Vector3ParameterNoInterpolation channelMixerBlue = new(Vector3.forward);

    /// <summary> Shadow color adjust. </summary>
    public ColorParameterNoInterpolation shadows = new(Color.white);

    /// <summary> Midtone color adjust. </summary>
    public ColorParameterNoInterpolation midtones = new(Color.white);

    /// <summary> Highlights color adjust. </summary>
    public ColorParameterNoInterpolation highlights = new(Color.white);

    /// <summary> Shadow range start [0, 2]. Default 0. </summary>
    [FloatSliderWithReset(0.0f, 0.0f, 2.0f)]
    public FloatSliderParameterNoInterpolation shadowsStart = new(0.0f, 0.0f, 2.0f);

    /// <summary> Shadow range end [0, 2]. Default 0.3. </summary>
    [FloatSliderWithReset(0.3f, 0.0f, 2.0f)]
    public FloatSliderParameterNoInterpolation shadowsEnd = new(0.3f, 0.0f, 2.0f);

    /// <summary> Highlights range start [0, 2]. Default 0.55. </summary>
    [FloatSliderWithReset(0.55f, 0.0f, 2.0f)]
    public FloatSliderParameterNoInterpolation highlightsStart = new(0.55f, 0.0f, 2.0f);

    /// <summary> Highlights range end [0, 2]. Default 1. </summary>
    [FloatSliderWithReset(1.0f, 0.0f, 2.0f)]
    public FloatSliderParameterNoInterpolation highLightsEnd = new(1.0f, 0.0f, 2.0f);

    #endregion
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Advanced settings.
    
    /// <summary> Does it affect the Scene View? </summary>
    [ToggleWithReset(false)]
    public BoolParameterNoInterpolation affectSceneView = new(false);

    #endregion
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary> Reset the volume to default values. </summary>
    public void Reset()
    {
      intensity.value = 1.0f;

      mode.value = Cyberpunk.Modes.Quality;

      tonemap.value = Cyberpunk.Tonemappers.None;
      exposure.value = 0.0f;
      contrast.value = 0.0f;
      tint.value = Color.white;
      hue.value = 0.0f;
      saturation.value = 0.0f;
      vibrance.value = 0.0f;
      whiteTemperature.value = 0.0f;
      whiteTint.value = 0.0f;
      splitToningShadows.value = Color.gray;
      splitToningHighlights.value = Color.gray;
      splitToningBalance.value = 0.0f;
      channelMixerRed.value = Vector3.right;
      channelMixerGreen.value = Vector3.up;
      channelMixerBlue.value = Vector3.forward;
      shadows.value = midtones.value = highlights.value = Color.white;
      shadowsStart.value = 0.0f;
      shadowsEnd.value = 0.3f;
      highlightsStart.value = 0.55f;
      highLightsEnd.value = 1.0f;

      affectSceneView.value = false;
    }

    /// <summary> Checks if the volume is active. </summary>
    /// <returns> True if the volume is active, false otherwise. </returns>
    public bool IsActive()
    {
      if (intensity.overrideState == false || intensity.value <= 0.0f || profile.value == null)
        return false;

      Texture3D texture3D = mode.value == Cyberpunk.Modes.Quality ? profile.value.quality : profile.value.performance;

      return texture3D != null;
    }

    /// <summary> Checks if the volume is tile compatible. </summary>
    /// <returns> True if the volume is tile compatible, false otherwise. </returns>
    public bool IsTileCompatible() => false;
  }
}
