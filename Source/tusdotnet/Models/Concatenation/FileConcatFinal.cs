using System.Linq;

namespace tusdotnet.Models.Concatenation
{
	public class FileConcatFinal : FileConcat
	{
		public string[] Files { get; set; }

		public FileConcatFinal(params string[] partialFiles)
		{
			Files = partialFiles;
		}

		public override string GetHeader()
		{
			return $"final;{string.Join(" ", Files)}";
		}

		internal void AddUrlPathToFiles(string urlPath)
		{
			Files = Files.Select(file => $"{urlPath.TrimEnd('/')}/{file}").ToArray();
		}
	}
}
