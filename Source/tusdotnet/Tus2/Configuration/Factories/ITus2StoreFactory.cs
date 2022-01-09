using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public interface ITus2StoreFactory
    {
        public Task<ITus2Store> Create(HttpContext httpContext);
    }
}
