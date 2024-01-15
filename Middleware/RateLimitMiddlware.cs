using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ratelimiter
{
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        static readonly ConcurrentDictionary<string, DateTime?> ApiCallsInMemory = new();
        public RateLimitMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var controllerActionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();

            if(controllerActionDescriptor is null)
            {
                await _next(context);
                return;
            }

            var apiDecorator = (RateLimiterDecorator)controllerActionDescriptor.MethodInfo
            .GetCustomAttributes(true).SingleOrDefault(x => x.GetType() == typeof(RateLimiterDecorator));

            if(apiDecorator is null)
            {
                await _next(context);
                return;
            }

            string key = GetCurrentClientKey(apiDecorator, context);

            var previousApiCall = GetPreviousApiCallsByKey(key);
            if (previousApiCall != null)
            {
                if (DateTime.Now < previousApiCall.Value.AddSeconds(5))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    return;
                }
            }

            UpdateApiCallKey(key);
            await _next(context);
            
        }

        private static string GetCurrentClientKey(RateLimiterDecorator apiDecorator, HttpContext context)
        {
            var keys = new List<string>
            {
                context.Request.Path
            };

            if (apiDecorator.StrategyType == StrategyTypeEnum.IpAddress)
                keys.Add(GetClientIpAddress(context));

            // implement other strategy
            return string.Join('_', keys);

        }

        private static string GetClientIpAddress(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString();
        }

        private DateTime? GetPreviousApiCallsByKey(string key)
        {
            ApiCallsInMemory.TryGetValue(key, out DateTime? value);
            return value;
        }

        private void UpdateApiCallKey(string key)
        {
            ApiCallsInMemory.TryRemove(key, out _);
            ApiCallsInMemory.TryAdd(key, DateTime.Now);
        }

    }
}