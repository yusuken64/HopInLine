namespace HopInLine.Data.Line
{
	public class LineChangedEventArgs
	{
		public Line line;

		public LineChangedEventArgs(Line line)
		{
			this.line = line;
		}
	}
}