﻿namespace DeepNestLib.Placement
{
  using System;

  public class PartPlacement
  {
    public PartPlacement(NFP part)
    {
      this.Part = part;
    }

    public double? mergedLength
    {
      get
      {
        return null;
      }

      set
      {
        throw new NotImplementedException();
      }
    }

    public object mergedSegments
    {
      get
      {
        return null;
      }

      set
      {
        throw new NotImplementedException();
      }
    }

    public int id { get; set; }

    /// <summary>
    /// A hull of the part captured only when not Gravity or BoundingBox (ie. Squeeze).
    /// </summary>
    public NFP hull { get; set; }

    /// <summary>
    /// A hull of the sheet captured only when not Gravity or BoundingBox (ie. Squeeze).
    /// </summary>
    public NFP hullsheet { get; set; }

    /// <summary>
    /// Rotation of the part (sheets I don't think ever get rotated, so this would be absolute).
    /// </summary>
    public float rotation { get; set; }

    /// <summary>
    /// Offset of the part relative to the sheet.
    /// </summary>
    public double x { get; set; }

    /// <summary>
    /// Offset of the part relative to the sheet.
    /// </summary>
    public double y { get; set; }

    /// <summary>
    /// Source of the part placed.
    /// </summary>
    public int source { get; set; }

    public NFP Part { get; }
  }
}
