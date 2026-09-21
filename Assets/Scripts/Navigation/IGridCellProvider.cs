namespace TinyKingdom.Navigation
{
    public interface IGridCellProvider
    {
        int Width { get; }
        int Height { get; }
        bool IsInsideGrid(int gridX, int gridY);
        GridCell GetCell(int gridX, int gridY);
    }
}
