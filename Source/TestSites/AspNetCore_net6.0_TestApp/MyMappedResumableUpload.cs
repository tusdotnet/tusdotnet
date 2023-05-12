using System.Reflection;
using System.Text;
using tusdotnet.ModelBinders;

namespace AspNetCore_net6._0_TestApp
{
    public class MyMappedResumableUpload : ResumableUpload
    {
        public string FileName => Metadata["name"].GetString(Encoding.UTF8);

        public long DataLength => Content.Length;

        public static async ValueTask<MyMappedResumableUpload> BindAsync(HttpContext context, ParameterInfo _)
        {
            return await CreateAndBindFromHttpContext<MyMappedResumableUpload>(context);
        }
    }
}