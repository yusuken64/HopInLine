
namespace HopInLine.Data.Line
{
	public class LineChangedEventArgs
	{
		public Line line;
		public int updateId;

		public LineChangedEventArgs(Line line)
		{
			this.line = line;
		}
	}
}