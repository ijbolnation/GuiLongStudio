namespace AssetStudio
{
    /// <summary>
    /// Cubemap (class 89) is serialized like Texture2D; image_data holds the six faces (+X, -X, +Y, -Y, +Z, -Z).
    /// </summary>
    public sealed class Cubemap : Texture2D
    {
        public Cubemap(ObjectReader reader) : base(reader) { }
    }
}
