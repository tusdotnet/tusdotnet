using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Parsers;

namespace tusdotnet.Validation.Requirements
{
    internal class UploadSecretForPost : Requirement
    {
        private readonly ITusChallengeStore _challengeStore;

        public UploadSecretForPost(ITusChallengeStore challengeStore)
        {
            _challengeStore = challengeStore;
        }

        public override async Task Validate(ContextAdapter context)
        {
            // TODO: Return error if UploadSecret is provided but store does not support it?
            if (_challengeStore == null)
                return;

            var secret = context.Request.GetHeader(HeaderConstants.UploadSecret);

            if (string.IsNullOrWhiteSpace(secret))
                return;

            if(secret.Length < 48 || secret.Length > 256)
            {
                await BadRequest("Upload-Secret must have a length between 48 and 256 characters");
                return;
            }

            foreach (var c in secret)
            {
                if(!HeaderConstants.ValidUploadSecretChars.Contains(c))
                {
                    await BadRequest("Upload-Secret contains invalid characters");
                    return;
                }
            }
        }
    }
}
