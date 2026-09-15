namespace DragonBound.Presentation
{
    /// <summary>
    /// Centralized editor/project paths. Runtime UI loading must use UiAssets instead.
    /// Keeping authoring paths here prevents future folder moves from spreading through tests and tools.
    /// </summary>
    public static class UiVariantProjectPaths
    {
        public const string V1Root = "Assets/DragonBound/UI/Variants/V1";
        public const string V2Root = "Assets/DragonBound/UI/Variants/V2";

        public static string V1Scene(string name) => $"{V1Root}/Scenes/{name}.unity";
        public static string V1Ui(string relativePath) => $"{V1Root}/Content/UI/{relativePath.TrimStart('/')}";
        public static string V1Resource(string relativePath) =>
            $"{V1Root}/Content/Resources/{relativePath.TrimStart('/')}";
    }
}
