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
using System;

namespace FronkonGames.LUTs.Synthwave
{
  [AttributeUsage(AttributeTargets.Field)]
  public class FloatSliderWithResetAttribute : PropertyAttribute
  {
    public readonly float min;
    public readonly float max;
    public readonly float value;
    public readonly string tooltip;

    public FloatSliderWithResetAttribute(float value, float min, float max, string tooltip = "")
    {
      Debug.Assert(value >= min && value <= max, "Value is out of range");
      Debug.Assert(min <= max, "Min must be less than or equal to max");

      this.value = value;
      this.min = min;
      this.max = max;
      this.tooltip = tooltip;
    }
  }

  [AttributeUsage(AttributeTargets.Field)]
  public class Vector3WithResetAttribute : PropertyAttribute
  {
    public readonly Vector3 value;
    public readonly string tooltip;

    public Vector3WithResetAttribute(float x, float y, float z, string tooltip = "")
    {
      this.value = new Vector3(x, y, z);
      this.tooltip = tooltip;
    }
  }

  [AttributeUsage(AttributeTargets.Field)]
  public class ToggleWithResetAttribute : PropertyAttribute
  {
    public readonly bool value;
    public readonly string tooltip;

    public ToggleWithResetAttribute(bool value, string tooltip = "")
    {
      this.value = value;
      this.tooltip = tooltip;
    }
  }
}
