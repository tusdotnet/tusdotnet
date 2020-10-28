using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions.Internal;
using tusdotnet.Interfaces;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Validation.Requirements
{
    internal class ClientTagForHead : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            if (!context.Configuration.SupportsClientTag())
            {
                if (string.IsNullOrWhiteSpace(context.Request.FileId))
                {
                    await NotFound();
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(context.Request.FileId) && string.IsNullOrWhiteSpace(context.Request.GetHeader(HeaderConstants.UploadTag)))
            {
                await NotFound();
                return;
            }

            if (!string.IsNullOrWhiteSpace(context.Request.FileId))
            {
                return;
            }

            var clientTagStore = (ITusClientTagStore)context.Configuration.Store;

            var uploadTag = context.Request.GetHeader(HeaderConstants.UploadTag);
            var fileIdMap = await clientTagStore.ResolveUploadTagToFileIdAsync(uploadTag);

            if (string.IsNullOrWhiteSpace(fileIdMap?.FileId))
            {
                await NotFound();
                return;
            }

            // TODO: Run through EventHelper?
            // TODO: Is this the correct place?
            var resolveClientTagContext = ResolveClientTagContext.Create(context, ctx =>
            {
                ctx.UploadTag = uploadTag;
                ctx.RequestPassesChallenge = context.Request.UploadChallengeProvidedAndPassed;
                ctx.ClientTagBelongsToCurrentUser = fileIdMap.User == context.GetUsername();
            });
            await context.Configuration.Events.OnResolveClientTagAsync(resolveClientTagContext);

            if (!resolveClientTagContext.RequestIsAllowed)
            {
                await NotFound();
                return;
            }

            context.Request.SetFileId(fileIdMap.FileId);
        }
    }
}
