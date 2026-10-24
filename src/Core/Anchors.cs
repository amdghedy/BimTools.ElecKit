using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class Anchors
    {
        /// <summary>The element's location point, or the centre of its bounding box. Null if it has neither.</summary>
        public static XYZ Of(Element element)
        {
            var point = element.Location as LocationPoint;
            if (point != null) return point.Point;

            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return null;
            return box.Min.Add(box.Max).Multiply(0.5);
        }
    }
}
