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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;

namespace FronkonGames.LUTs.Cyberpunk.Editor
{
  public abstract partial class Inspector : VolumeComponentEditor
  {
    protected void DrawFloatSliderWithReset(string name, string label = null)
    {
      SerializedDataParameter parameter = UnpackParameter(name);
      if (parameter == null)
        return;

      var attr = parameter.GetAttribute<FloatSliderWithResetAttribute>();

      if (attr == null)
      {
        EditorGUILayout.PropertyField(parameter.value, new GUIContent(label ?? parameter.displayName));
        return;
      }

      GUIContent displayLabel = new(label ?? parameter.displayName, attr.tooltip);

      EditorGUILayout.BeginHorizontal();
      {
        EditorGUI.showMixedValue = parameter.overrideState.hasMultipleDifferentValues;
        EditorGUI.showMixedValue = false;

        bool isOverridden = parameter.overrideState.boolValue;
        EditorGUI.BeginDisabledGroup(!isOverridden);

        EditorGUILayout.BeginHorizontal();
        {
          float value = parameter.value.floatValue;

          value = EditorGUILayout.Slider(displayLabel, value, attr.min, attr.max);

          EditorGUI.EndDisabledGroup();
          int oldIndentLevel = EditorGUI.indentLevel;
          EditorGUI.indentLevel = 0;
          EditorGUILayout.PropertyField(parameter.overrideState, GUIContent.none, GUILayout.Width(16));
          EditorGUI.indentLevel = oldIndentLevel;
          EditorGUI.BeginDisabledGroup(!isOverridden);

          if (ResetButton(attr.value, value != attr.value) == true)
            value = attr.value;

          parameter.value.floatValue = value;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
      }
      EditorGUILayout.EndHorizontal();
    }

    protected void DrawToggleWithReset(string name, string label = null, bool defaultValue = default)
    {
      SerializedDataParameter parameter = UnpackParameter(name);
      if (parameter == null)
        return;

      GUIContent displayLabel = new(label ?? parameter.displayName);

      EditorGUILayout.BeginHorizontal();
      {
        EditorGUI.showMixedValue = parameter.overrideState.hasMultipleDifferentValues;
        EditorGUI.showMixedValue = false;

        bool isOverridden = parameter.overrideState.boolValue;
        EditorGUI.BeginDisabledGroup(!isOverridden);

        EditorGUILayout.BeginHorizontal();
        {
          bool value = parameter.value.boolValue;

          EditorGUI.BeginChangeCheck();
          value = EditorGUILayout.Toggle(displayLabel, value);

          EditorGUI.EndDisabledGroup();
          int oldIndentLevel = EditorGUI.indentLevel;
          EditorGUI.indentLevel = 0;
          EditorGUILayout.PropertyField(parameter.overrideState, GUIContent.none, GUILayout.Width(16));
          EditorGUI.indentLevel = oldIndentLevel;
          EditorGUI.BeginDisabledGroup(!isOverridden);

          bool isDefault = EqualityComparer<bool>.Default.Equals(value, defaultValue);
          if (ResetButton(defaultValue, !isDefault) == true)
            value = defaultValue;

          parameter.value.boolValue = value;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
      }
      EditorGUILayout.EndHorizontal();
    }

    protected void DrawEnumDropdownWithReset<T>(string name, string label = null, T defaultValue = default) where T : Enum
    {
      SerializedDataParameter parameter = UnpackParameter(name);
      if (parameter == null)
        return;

      GUIContent displayLabel = new(label ?? parameter.displayName);

      EditorGUILayout.BeginHorizontal();
      {
        EditorGUI.showMixedValue = parameter.overrideState.hasMultipleDifferentValues;
        EditorGUI.showMixedValue = false;

        bool isOverridden = parameter.overrideState.boolValue;
        EditorGUI.BeginDisabledGroup(!isOverridden);

        EditorGUILayout.BeginHorizontal();
        {
          int currentInt = parameter.value.intValue;

          Array enumValues = Enum.GetValues(typeof(T));
          if (currentInt < 0 || currentInt >= enumValues.Length)
            currentInt = 0;

          T currentValue = (T)enumValues.GetValue(currentInt);

          EditorGUI.BeginChangeCheck();
          T newValue = (T)EditorGUILayout.EnumPopup(displayLabel, currentValue);

          if (EditorGUI.EndChangeCheck() == true)
          {
            parameter.value.intValue = Convert.ToInt32(newValue);
          }

          EditorGUI.EndDisabledGroup();
          int oldIndentLevel = EditorGUI.indentLevel;
          EditorGUI.indentLevel = 0;
          EditorGUILayout.PropertyField(parameter.overrideState, GUIContent.none, GUILayout.Width(16));
          EditorGUI.indentLevel = oldIndentLevel;
          EditorGUI.BeginDisabledGroup(!isOverridden);

          bool isDefault = EqualityComparer<T>.Default.Equals(newValue, defaultValue);
          if (ResetButton(defaultValue, !isDefault) == true)
            parameter.value.intValue = Convert.ToInt32(defaultValue);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
      }
      EditorGUILayout.EndHorizontal();
    }

    protected void DrawVector3WithReset(string name, string label = null, Vector3 defaultValue = default)
    {
      SerializedDataParameter parameter = UnpackParameter(name);
      if (parameter == null)
        return;

      GUIContent displayLabel = new(label ?? parameter.displayName);

      Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

      float buttonsWidth = 16.0f + 20.0f + 2.0f;
      Rect fieldRect = new(rect.x, rect.y, rect.width - buttonsWidth, rect.height);
      Rect overrideRect = new(rect.x + rect.width - buttonsWidth + 2.0f, rect.y, 16.0f, rect.height);
      Rect resetRect = new(rect.x + rect.width - 19.0f, rect.y, 19.0f, rect.height);

      EditorGUI.showMixedValue = parameter.overrideState.hasMultipleDifferentValues;
      EditorGUI.showMixedValue = false;

      bool isOverridden = parameter.overrideState.boolValue;

      EditorGUI.BeginDisabledGroup(!isOverridden);
      Vector3 value = parameter.value.vector3Value;
      EditorGUI.BeginChangeCheck();

      Rect labelRect = new(fieldRect.x, fieldRect.y, EditorGUIUtility.labelWidth, fieldRect.height);
      Rect valueRect = new(fieldRect.x + EditorGUIUtility.labelWidth, fieldRect.y, fieldRect.width - EditorGUIUtility.labelWidth, fieldRect.height);

      EditorGUI.LabelField(labelRect, displayLabel);
      value = EditorGUI.Vector3Field(valueRect, GUIContent.none, value);

      if (EditorGUI.EndChangeCheck() == true)
        parameter.value.vector3Value = value;
      EditorGUI.EndDisabledGroup();

      int oldIndentLevel = EditorGUI.indentLevel;
      EditorGUI.indentLevel = 0;
      EditorGUI.PropertyField(overrideRect, parameter.overrideState, GUIContent.none);
      EditorGUI.indentLevel = oldIndentLevel;

      EditorGUI.BeginDisabledGroup(!isOverridden);
      if (ResetButton(resetRect, defaultValue, value != defaultValue) == true)
        parameter.value.vector3Value = defaultValue;
      EditorGUI.EndDisabledGroup();
    }

    /// <summary> Draws a MinMaxSlider for two float parameters with reset. </summary>
    protected void DrawMinMaxSliderWithReset(string minName, string maxName, string label, float minLimit, float maxLimit, Vector2 defaultRange)
    {
      SerializedDataParameter minParam = UnpackParameter(minName);
      SerializedDataParameter maxParam = UnpackParameter(maxName);
      if (minParam == null || maxParam == null)
        return;

      EditorGUILayout.BeginHorizontal();
      {
        float minValue = minParam.value.floatValue;
        float maxValue = maxParam.value.floatValue;

        EditorGUI.showMixedValue = minParam.overrideState.hasMultipleDifferentValues || maxParam.overrideState.hasMultipleDifferentValues;

        bool isOverridden = minParam.overrideState.boolValue || maxParam.overrideState.boolValue;
        EditorGUI.BeginDisabledGroup(!isOverridden);

        EditorGUILayout.MinMaxSlider(new GUIContent(label, $"[{minValue:0.0}..{maxValue:0.0}]"), ref minValue, ref maxValue, minLimit, maxLimit);

        EditorGUI.EndDisabledGroup();

        int oldIndentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        EditorGUI.BeginChangeCheck();
        bool newOverrideState = EditorGUILayout.Toggle(GUIContent.none, isOverridden, GUILayout.Width(16));
        if (EditorGUI.EndChangeCheck() == true)
        {
          minParam.overrideState.boolValue = newOverrideState;
          maxParam.overrideState.boolValue = newOverrideState;
          isOverridden = newOverrideState;
        }
        EditorGUI.indentLevel = oldIndentLevel;
        EditorGUI.showMixedValue = false;

        EditorGUI.BeginDisabledGroup(!isOverridden);
        if (ResetButton(defaultRange, minValue != defaultRange.x || maxValue != defaultRange.y) == true)
        {
          minValue = defaultRange.x;
          maxValue = defaultRange.y;
        }

        minParam.value.floatValue = minValue;
        maxParam.value.floatValue = maxValue;
        EditorGUI.EndDisabledGroup();
      }
      EditorGUILayout.EndHorizontal();
    }

    protected void DrawColorWithReset(string name, string label = null, Color? defaultValue = null)
    {
      SerializedDataParameter parameter = UnpackParameter(name);
      if (parameter == null)
        return;

      GUIContent displayLabel = new(label ?? parameter.displayName);

      EditorGUILayout.BeginHorizontal();
      {
        EditorGUI.showMixedValue = parameter.overrideState.hasMultipleDifferentValues;
        EditorGUI.showMixedValue = false;

        bool isOverridden = parameter.overrideState.boolValue;
        EditorGUI.BeginDisabledGroup(!isOverridden);

        EditorGUILayout.BeginHorizontal();
        {
          Color value = parameter.value.colorValue;

          EditorGUI.BeginChangeCheck();
          value = EditorGUILayout.ColorField(displayLabel, value);

          EditorGUI.EndDisabledGroup();
          int oldIndentLevel = EditorGUI.indentLevel;
          EditorGUI.indentLevel = 0;
          EditorGUILayout.PropertyField(parameter.overrideState, GUIContent.none, GUILayout.Width(16));
          EditorGUI.indentLevel = oldIndentLevel;
          EditorGUI.BeginDisabledGroup(!isOverridden);

          Color resetVal = defaultValue ?? value;
          if (ResetButton(resetVal, value != resetVal) == true)
            value = resetVal;

          parameter.value.colorValue = value;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
      }
      EditorGUILayout.EndHorizontal();
    }

    protected void DrawProfileWithReset(string name, string label = null)
    {
      SerializedDataParameter parameter = UnpackParameter(name);
      if (parameter == null)
        return;

      GUIContent displayLabel = new(label ?? parameter.displayName);

      EditorGUILayout.BeginHorizontal();
      {
        EditorGUI.showMixedValue = parameter.overrideState.hasMultipleDifferentValues;
        EditorGUI.showMixedValue = false;

        bool isOverridden = parameter.overrideState.boolValue;
        EditorGUI.BeginDisabledGroup(!isOverridden);

        EditorGUILayout.BeginHorizontal();
        {
          UnityEngine.Object value = parameter.value.objectReferenceValue;

          EditorGUI.BeginChangeCheck();
          value = EditorGUILayout.ObjectField(displayLabel, value, typeof(Profile), false);

          EditorGUI.EndDisabledGroup();
          int oldIndentLevel = EditorGUI.indentLevel;
          EditorGUI.indentLevel = 0;
          EditorGUILayout.PropertyField(parameter.overrideState, GUIContent.none, GUILayout.Width(16));
          EditorGUI.indentLevel = oldIndentLevel;
          EditorGUI.BeginDisabledGroup(!isOverridden);

          if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), EditorStyles.miniLabel, GUILayout.Width(19.0f)) == true && value != null)
            value = null;

          if (EditorGUI.EndChangeCheck() == true || parameter.value.objectReferenceValue != value)
            parameter.value.objectReferenceValue = value;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
      }
      EditorGUILayout.EndHorizontal();
    }

    protected T GetAttribute<T>(SerializedDataParameter param) where T : Attribute
    {
      if (param.attributes == null)
        return null;

      return param.attributes.OfType<T>().FirstOrDefault();
    }
  }
}
