using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace ElecKit.Core
{
    public static class RoomLookup
    {
        /// <summary>The room a family instance is in: Revit's own answer first, then a point test at its location.</summary>
        public static Room Of(Document doc, FamilyInstance instance)
        {
            Room room = instance.Room;
            if (room != null) return room;

            XYZ point = Anchors.Of(instance);
            return point == null ? null : doc.GetRoomAtPoint(point);
        }

        public static string NameOf(Room room)
        {
            Parameter p = room.get_Parameter(BuiltInParameter.ROOM_NAME);
            string value = p != null ? p.AsString() : null;
            return value ?? "";
        }
    }
}
