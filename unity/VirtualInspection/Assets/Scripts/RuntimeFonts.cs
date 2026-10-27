using UnityEngine;

namespace VirtualInspection
{
    public static class RuntimeFonts
    {
        private static Font _font;

        public static Font Get()
        {
            if (_font != null)
            {
                return _font;
            }

            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Arial" },
                20);
            if (_font == null)
            {
                try
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch
                {
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }
            return _font;
        }
    }
}
