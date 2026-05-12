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

namespace FronkonGames.LUTs.Synthwave.Editor
{
  public abstract partial class Inspector
  {
    public static void Separator(float space = 0.0f)
    {
      if (space <= 0.0f)
        EditorGUILayout.Separator();
      else
        GUILayout.Space(space);
    }

    public static bool Button(string label, string tooltip = default, GUIStyle style = null) => GUILayout.Button(new GUIContent(label, tooltip), style ?? GUI.skin.button);
    public static bool MiniButton(string label, string tooltip = default) => GUILayout.Button(new GUIContent(label, tooltip), Styles.miniLabelButton);

    public static bool ResetButton<T>(T resetValue, bool enabled = true)
    {
      GUI.enabled = enabled;
      bool reset = GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), EditorStyles.miniLabel, GUILayout.Width(19.0f));
      GUI.enabled = true;
      return reset;
    }

    public static bool ResetButton() => GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), EditorStyles.miniLabel, GUILayout.Width(19.0f));

    public static bool ResetButton<T>(Rect rect, T resetValue, bool enabled = true)
    {
      bool reset = false;
      bool oldEnabled = GUI.enabled;
      GUI.enabled = enabled;

      if (GUI.Button(rect, EditorGUIUtility.IconContent("Refresh"), EditorStyles.miniLabel) == true)
        reset = true;

      GUI.enabled = oldEnabled;

      return reset;
    }

    public static bool Foldout(string title)
    {
      bool display = GetFoldoutDisplay(title);

      Rect rect = GUILayoutUtility.GetRect(16.0f, 22.0f, Styles.HeaderStyle);
      GUI.Box(rect, title, Styles.HeaderStyle);

      Rect toggleRect = new(rect.x + 4.0f, rect.y + 2.0f, 13.0f, 13.0f);
      if (Event.current.type == EventType.Repaint)
        EditorStyles.foldout.Draw(toggleRect, false, false, display, false);

      Event e = Event.current;
      if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) == true)
      {
        display = !display;
        e.Use();
      }

      SetFoldoutDisplay(title, display);

      return display;
    }

    private static bool GetFoldoutDisplay(string foldoutName)
    {
      string key = $"{Constants.Asset.AssemblyName}.display{foldoutName}";
      bool value = true;

      if (foldoutDisplay.ContainsKey(key) == false)
      {
        value = PlayerPrefs.GetInt(key, 0) == 1;
        foldoutDisplay.Add(key, value);
      }
      else
        value = foldoutDisplay[key];

      return value;
    }

    private static void SetFoldoutDisplay(string foldoutName, bool value)
    {
      string key = $"{Constants.Asset.AssemblyName}.display{foldoutName}";

      if (foldoutDisplay.ContainsKey(key) == false)
        foldoutDisplay.Add(key, value);
      else
        foldoutDisplay[key] = value;

      PlayerPrefs.SetInt(key, value == true ? 1 : 0);
    }
  }
}
