namespace tusdotnet.Models.Concatenation
{
	public class FileConcatPartial : FileConcat
	{
		public override string GetHeader()
		{
			return "partial";
		}
	}
}
