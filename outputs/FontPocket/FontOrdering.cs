using System;
using System.Collections.Generic;
using System.Linq;

namespace FontPocket {
    public static class FontOrdering {
        public static string Family(ManagedFont font) { return font.Metadata!=null && font.Metadata.Faces.Count>0 ? font.Metadata.Faces[0].Family : font.Name; }
        public static int Weight(ManagedFont font) { return font.Metadata!=null && font.Metadata.Faces.Count>0 ? font.Metadata.Faces[0].Weight : 400; }
        public static IOrderedEnumerable<ManagedFont> Order(IEnumerable<ManagedFont> fonts) {
            return fonts.OrderBy(Family,StringComparer.CurrentCultureIgnoreCase).ThenBy(Weight)
                .ThenBy(f=>f.Metadata!=null && f.Metadata.Faces.Count>0 ? f.Metadata.Faces[0].Style : "",StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(f=>f.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(f=>f.Path,StringComparer.OrdinalIgnoreCase);
        }
    }
}
