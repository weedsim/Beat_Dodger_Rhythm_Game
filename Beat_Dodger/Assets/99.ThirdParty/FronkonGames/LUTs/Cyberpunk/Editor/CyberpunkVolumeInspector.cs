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
using UnityEditor;

namespace FronkonGames.LUTs.Cyberpunk.Editor
{
  [CustomEditor(typeof(CyberpunkVolume))]
  public class CyberpunkVolumeInspector : Inspector
  {
    protected override void InspectorGUI()
    {
      /////////////////////////////////////////////////
      // Common.
      /////////////////////////////////////////////////
      DrawFloatSliderWithReset("intensity");

      Separator();

      /////////////////////////////////////////////////
      // Cyberpunk.
      /////////////////////////////////////////////////
      DrawEnumDropdownWithReset("mode", "Mode", Cyberpunk.Modes.Quality);
      DrawProfileWithReset("profile", "LUT Profile");
      DrawEnumDropdownWithReset("tonemap", "Tonemap", Cyberpunk.Tonemappers.None);

      Separator();

      /////////////////////////////////////////////////
      // Color grading.
      /////////////////////////////////////////////////
      if (Foldout("Color grading") == true)
      {
        IndentLevel++;
        DrawFloatSliderWithReset("exposure", "Exposure");
        DrawFloatSliderWithReset("contrast", "Contrast");
        DrawColorWithReset("tint", "Tint", Color.white);
        DrawFloatSliderWithReset("hue", "HUE");
        DrawFloatSliderWithReset("saturation", "Saturation");
        DrawFloatSliderWithReset("vibrance", "Vibrance");

        EditorGUILayout.LabelField("White balance");
        IndentLevel++;
        DrawFloatSliderWithReset("whiteTemperature", "Temperature");
        DrawFloatSliderWithReset("whiteTint", "Tint");
        IndentLevel--;
        IndentLevel--;
      }

      /////////////////////////////////////////////////
      // Split toning.
      /////////////////////////////////////////////////
      if (Foldout("Split toning") == true)
      {
        IndentLevel++;
        DrawColorWithReset("splitToningShadows", "Shadows", Color.gray);
        DrawColorWithReset("splitToningHighlights", "Highlights", Color.gray);
        DrawFloatSliderWithReset("splitToningBalance", "Balance");
        IndentLevel--;
      }

      /////////////////////////////////////////////////
      // Channel mixer.
      /////////////////////////////////////////////////
      if (Foldout("Channel mixer") == true)
      {
        IndentLevel++;
        DrawVector3WithReset("channelMixerRed", "Red", Vector3.right);
        DrawVector3WithReset("channelMixerGreen", "Green", Vector3.up);
        DrawVector3WithReset("channelMixerBlue", "Blue", Vector3.forward);
        IndentLevel--;
      }

      /////////////////////////////////////////////////
      // Shadows Midtones Highlights.
      /////////////////////////////////////////////////
      if (Foldout("Shadows Midtones Highlights") == true)
      {
        IndentLevel++;
        DrawColorWithReset("shadows", "Shadows", Color.white);
        IndentLevel++;
        DrawMinMaxSliderWithReset("shadowsStart", "shadowsEnd", "Range", 0.0f, 2.0f, new Vector2(0.0f, 0.3f));
        IndentLevel--;
        DrawColorWithReset("midtones", "Midtones", Color.white);
        DrawColorWithReset("highlights", "Highlights", Color.white);
        IndentLevel++;
        DrawMinMaxSliderWithReset("highlightsStart", "highLightsEnd", "Range", 0.0f, 2.0f, new Vector2(0.55f, 1.0f));
        IndentLevel--;
        IndentLevel--;
      }
    }

    protected override void ResetValues() => ((CyberpunkVolume)target).Reset();

    protected override void CheckForErrors()
    {
      if (Cyberpunk.IsInAnyRenderFeatures() == false)
      {
        Separator();
        EditorGUILayout.HelpBox($"Renderer Feature '{Constants.Asset.Name}' not found. You must add it as a Render Feature.", MessageType.Error);
      }
      else
      {
        Cyberpunk[] effects = Cyberpunk.Instances;
        bool anyEnabled = false;
        for (int i = 0; i < effects.Length; i++)
        {
          if (effects[i].isActive == true)
          {
            anyEnabled = true;
            break;
          }
        }

        if (anyEnabled == false)
        {
          Separator();
          EditorGUILayout.HelpBox($"No Renderer Feature '{Constants.Asset.Name}' is active. You must activate it in the Render Features.", MessageType.Warning);
        }
      }
    }
  }
}
