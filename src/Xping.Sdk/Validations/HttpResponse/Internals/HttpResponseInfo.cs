/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.HttpResponse.Internals
{
    internal class HttpResponseInfo(
        HttpResponseMessage response, 
        TimeSpan responseTime,
        TestContext context) : IHttpResponse
    {
        private readonly HttpResponseMessage _response = response.RequireNotNull(nameof(response));
        private readonly TestContext _context = context.RequireNotNull(nameof(context));
        private readonly TimeSpan _responseTime = responseTime;

        public HttpResponseMessage Response => _response;
        public TestContext Context => _context;
        public TimeSpan ResponseTime => _responseTime;
    }
}
