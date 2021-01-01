
namespace txte
{
    struct Point
    {
        public Point(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        public int X;
        public int Y;

        public override string ToString() => $"({this.X}, {this.Y})";
    }

    struct Size
    {
        public Size(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        public int Width;
        public int Height;

        public override string ToString() => $"({this.Width}, {this.Height})";
    }
}