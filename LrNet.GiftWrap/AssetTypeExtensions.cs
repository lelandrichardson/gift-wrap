using System;
using System.IO;

namespace LrNet.GiftWrap
{
    internal static class AssetTypeExtensions
    {
        internal static string FileExtension(this AssetType type)
        {
            switch (type)
            {
                case AssetType.JavaScript: return ".js";
                case AssetType.Css: return ".css";
                case AssetType.Less: return ".css";
                default: return "";
            }
        }

        internal static string ConcatenationToken(this AssetType type)
        {
            switch (type)
            {
                case AssetType.JavaScript: return ";" + Environment.NewLine;
                case AssetType.Css:
                case AssetType.Less:
                default: return Environment.NewLine;
            }
        }

        internal static AssetType ToAssetType(this string path)
        {
            var ext = Path.GetExtension(path);
            switch (ext)
            {
                case ".js": return AssetType.JavaScript;
                case ".css": return AssetType.Css;
                case ".less": return AssetType.Less;
                default: return AssetType.UNKNOWN;
            }
        }
    }
}