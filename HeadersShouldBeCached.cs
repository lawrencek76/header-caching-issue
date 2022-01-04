using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;



public class HeadersShouldBeCached
{
    [Fact]
    public async Task HeadersAddedOnStarting_AreCached()
    {
        // Arrange
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddResponseCaching();
            })
            .Configure(app =>
            {
                app.UseResponseCaching();
                app.Use(async (context, next) =>
                {
                    //cannot know if header is needed yet have to delay as late as possible to check other headers
                    context.Response.ContentType.Should().BeNull();
                    context.Response.Headers.Add("removeme", "should not exist");
                    context.Response.OnStarting(CheckIfHeaderNeeded, context);
                    await next();
                });
                CacheableResponse(app);
            }));

        // Act
        var response1 = await server.CreateRequest("/").SendAsync("GET");
        var response2 = await server.CreateRequest("/").SendAsync("GET");

        response1.Headers.Contains("test").Should().BeTrue();
        response1.Headers.Contains("removeme").Should().BeFalse();
        response2.Headers.Contains("test").Should().BeTrue();
        response2.Headers.Contains("removeme").Should().BeFalse();

        var value1 = response1.Headers.GetValues("test")?.FirstOrDefault();
        var value2 = response2.Headers.GetValues("test")?.FirstOrDefault();
        // Assert
        value1.Should().NotBeNull();
        value2.Should().NotBeNull();

        // Headers should be identical even though each exec of CheckIfHeaderNeeded generates a unique value
        value1.Should().Be(value2);

        _expensiveCheck1.Should().Be(1);
    }

    private static void CacheableResponse(IApplicationBuilder app)
    {
        app.Run(async context =>
        {
            context.Response.GetTypedHeaders().CacheControl =
            new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
            {
                Public = true,
                MaxAge = TimeSpan.FromSeconds(60)
            };
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("Test response");
        });
    }

    private static int _expensiveCheck1 = 0;
    private Task CheckIfHeaderNeeded(object state)
    {
        var context = (HttpContext)state;
        _expensiveCheck1++;
        if (context.Response.ContentType == "text/html")
        {
            context.Response.Headers.Add("test", Guid.NewGuid().ToString());
            context.Response.Headers.Remove("removeme");
        }
        return Task.CompletedTask;
    }

}