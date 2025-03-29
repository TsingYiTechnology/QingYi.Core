namespace QingYi.Core.Compression.ColorCompression
{
    internal interface IColorCompression
    {
        int Width { get; set; }
        int Height { get; set; }

        void CreateImage(string path, int width, int height);
        void LoadImage(string path);
    }
}
