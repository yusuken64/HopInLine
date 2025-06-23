
namespace HopInLine.Data.Line
{
	public class LineChangedEventArgs
	{
		public LineDto line;
		public int updateId;

		public LineChangedEventArgs(Line line)
		{
			this.line = LineDto.FromLine(line);
		}
	}
}